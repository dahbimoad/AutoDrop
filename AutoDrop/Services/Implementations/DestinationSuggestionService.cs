using AutoDrop.Core.Constants;
using AutoDrop.Models;
using AutoDrop.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoDrop.Services.Implementations;

/// <summary>
/// Implementation of destination suggestion service.
/// Provides intelligent destination suggestions including user-defined rules and custom folders.
/// </summary>
public sealed class DestinationSuggestionService : IDestinationSuggestionService
{
    private readonly IRuleService _ruleService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<DestinationSuggestionService> _logger;
    private readonly IReadOnlyDictionary<string, string> _categoryToFolder;

    public DestinationSuggestionService(
        IRuleService ruleService, 
        ISettingsService settingsService,
        ILogger<DestinationSuggestionService> logger)
    {
        _ruleService = ruleService ?? throw new ArgumentNullException(nameof(ruleService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger;
        
        // Map categories to Windows known folders
        _categoryToFolder = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { FileCategories.Image, Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) },
            { FileCategories.Document, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) },
            { FileCategories.Video, Environment.GetFolderPath(Environment.SpecialFolder.MyVideos) },
            { FileCategories.Audio, Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) },
            { FileCategories.Archive, GetDownloadsFolder() },
            { FileCategories.Installer, GetDownloadsFolder() },
            { FileCategories.Unknown, Environment.GetFolderPath(Environment.SpecialFolder.Desktop) },
            { "Folder", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) }
        };
        
        _logger.LogDebug("DestinationSuggestionService initialized");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DestinationSuggestion>> GetSuggestionsAsync(DroppedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _logger.LogDebug("Getting suggestions for: {ItemName} (Category: {Category}, Extension: {Extension})", 
            item.Name, item.Category, item.Extension);

        var suggestions = new List<DestinationSuggestion>();

        // 1. Add pinned custom folders at top priority
        await AddCustomFolderSuggestionsAsync(suggestions);

        // 2. Check for user-defined rule
        if (!item.IsDirectory && !string.IsNullOrEmpty(item.Extension))
        {
            var rule = await _ruleService.GetRuleForExtensionAsync(item.Extension);
            if (rule != null && Directory.Exists(rule.Destination))
            {
                _logger.LogDebug("Found rule for {Extension}: {Destination}", item.Extension, rule.Destination);
                
                // Check if already added from custom folders
                if (!suggestions.Any(s => string.Equals(s.FullPath, rule.Destination, StringComparison.OrdinalIgnoreCase)))
                {
                    suggestions.Add(new DestinationSuggestion
                    {
                        DisplayName = GetFolderDisplayName(rule.Destination),
                        FullPath = rule.Destination,
                        IsRecommended = true,
                        IsFromRule = true,
                        Confidence = 100,
                        IconGlyph = GetIconForCategory(item.Category)
                    });
                }
                else
                {
                    // Mark existing suggestion as recommended since it matches a rule
                    var existing = suggestions.First(s => string.Equals(s.FullPath, rule.Destination, StringComparison.OrdinalIgnoreCase));
                    existing.IsRecommended = true;
                    existing.IsFromRule = true;
                    existing.Confidence = 100;
                }
            }
        }

        // 3. Add category-based default suggestion
        var defaultDestination = GetDefaultDestination(item.Category);
        if (!suggestions.Any(s => string.Equals(s.FullPath, defaultDestination, StringComparison.OrdinalIgnoreCase)))
        {
            suggestions.Add(new DestinationSuggestion
            {
                DisplayName = GetFolderDisplayName(defaultDestination),
                FullPath = defaultDestination,
                IsRecommended = !suggestions.Any(s => s.IsRecommended),
                IsFromRule = false,
                Confidence = 80,
                IconGlyph = GetIconForCategory(item.Category)
            });
        }

        // 4. Add common alternative destinations
        AddAlternativeDestinations(suggestions, item.Category);

        // Sort: Recommended first, then by confidence
        var result = suggestions
            .OrderByDescending(s => s.IsRecommended)
            .ThenByDescending(s => s.Confidence)
            .Take(AppConstants.MaxSuggestions)
            .ToList()
            .AsReadOnly();

        _logger.LogDebug("Returning {Count} suggestions for {ItemName}", result.Count, item.Name);
        return result;
    }

    /// <summary>
    /// Adds custom folders from settings as suggestions.
    /// Pinned folders appear first with high priority.
    /// </summary>
    private async Task AddCustomFolderSuggestionsAsync(List<DestinationSuggestion> suggestions)
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            var customFolders = settings.CustomFolders
                .Where(f => Directory.Exists(f.Path))
                .OrderByDescending(f => f.IsPinned)
                .ThenBy(f => f.Name);

            var addedCount = 0;
            foreach (var folder in customFolders)
            {
                if (suggestions.Count >= AppConstants.MaxSuggestions)
                    break;

                _logger.LogDebug("Adding custom folder suggestion: {Name} -> {Path}", folder.Name, folder.Path);
                
                suggestions.Add(new DestinationSuggestion
                {
                    DisplayName = folder.Name,
                    FullPath = folder.Path,
                    IsRecommended = folder.IsPinned,
                    IsFromRule = false,
                    Confidence = folder.IsPinned ? 95 : 75,
                    IconGlyph = "\uE838" // FolderFilled icon for custom folders
                });
                addedCount++;
            }
            
            _logger.LogDebug("Added {Count} custom folder suggestions", addedCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load custom folders for suggestions");
        }
    }

    /// <inheritdoc />
    public string GetDefaultDestination(string category)
    {
        return _categoryToFolder.TryGetValue(category, out var folder)
            ? folder
            : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    }

    private void AddAlternativeDestinations(List<DestinationSuggestion> suggestions, string category)
    {
        var alternatives = new[]
        {
            (Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Desktop", 60),
            (GetDownloadsFolder(), "Downloads", 50),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Documents", 40)
        };

        foreach (var (path, name, confidence) in alternatives)
        {
            if (suggestions.Count >= AppConstants.MaxSuggestions)
                break;

            if (suggestions.Any(s => string.Equals(s.FullPath, path, StringComparison.OrdinalIgnoreCase)))
                continue;

            suggestions.Add(new DestinationSuggestion
            {
                DisplayName = name,
                FullPath = path,
                IsRecommended = false,
                IsFromRule = false,
                Confidence = confidence,
                IconGlyph = GetIconForFolder(name)
            });
        }
    }

    private static string GetDownloadsFolder()
    {
        // Windows Known Folder for Downloads
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    private static string GetFolderDisplayName(string folderPath)
    {
        var knownFolders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Desktop" },
            { Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Documents" },
            { Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Pictures" },
            { Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Videos" },
            { Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Music" },
            { GetDownloadsFolder(), "Downloads" }
        };

        return knownFolders.TryGetValue(folderPath, out var displayName)
            ? displayName
            : Path.GetFileName(folderPath);
    }

    private static string GetIconForCategory(string category)
    {
        return category switch
        {
            FileCategories.Image => "\uE8B9",      // Photo
            FileCategories.Document => "\uE8A5",   // Document
            FileCategories.Video => "\uE714",      // Video
            FileCategories.Audio => "\uE8D6",      // Music
            FileCategories.Archive => "\uE8B7",    // Zip
            FileCategories.Installer => "\uE896",  // Download
            _ => "\uE8B7"                          // Folder
        };
    }

    private static string GetIconForFolder(string folderName)
    {
        return folderName switch
        {
            "Desktop" => "\uE8FC",
            "Documents" => "\uE8A5",
            "Downloads" => "\uE896",
            "Pictures" => "\uE8B9",
            "Videos" => "\uE714",
            "Music" => "\uE8D6",
            _ => "\uE8B7"
        };
    }
}
