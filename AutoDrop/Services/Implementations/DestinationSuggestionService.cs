using AutoDrop.Core.Constants;
using AutoDrop.Models;
using AutoDrop.Services.Interfaces;

namespace AutoDrop.Services.Implementations;

/// <summary>
/// Implementation of destination suggestion service.
/// </summary>
public sealed class DestinationSuggestionService : IDestinationSuggestionService
{
    private readonly IRuleService _ruleService;
    private readonly IReadOnlyDictionary<string, string> _categoryToFolder;

    public DestinationSuggestionService(IRuleService ruleService)
    {
        _ruleService = ruleService ?? throw new ArgumentNullException(nameof(ruleService));
        
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
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DestinationSuggestion>> GetSuggestionsAsync(DroppedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var suggestions = new List<DestinationSuggestion>();

        // Check for user-defined rule first
        if (!item.IsDirectory && !string.IsNullOrEmpty(item.Extension))
        {
            var rule = await _ruleService.GetRuleForExtensionAsync(item.Extension);
            if (rule != null && Directory.Exists(rule.Destination))
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
        }

        // Add category-based default suggestion
        var defaultDestination = GetDefaultDestination(item.Category);
        if (!suggestions.Any(s => string.Equals(s.FullPath, defaultDestination, StringComparison.OrdinalIgnoreCase)))
        {
            suggestions.Add(new DestinationSuggestion
            {
                DisplayName = GetFolderDisplayName(defaultDestination),
                FullPath = defaultDestination,
                IsRecommended = !suggestions.Any(),
                IsFromRule = false,
                Confidence = 80,
                IconGlyph = GetIconForCategory(item.Category)
            });
        }

        // Add common alternative destinations
        AddAlternativeDestinations(suggestions, item.Category);

        return suggestions
            .Take(AppConstants.MaxSuggestions)
            .ToList()
            .AsReadOnly();
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
