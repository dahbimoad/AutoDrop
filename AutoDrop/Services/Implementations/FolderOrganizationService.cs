using AutoDrop.Core.Constants;
using AutoDrop.Models;
using AutoDrop.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoDrop.Services.Implementations;

/// <summary>
/// Service for organizing files within a folder based on various criteria.
/// Implements rate limiting for AI operations and file size restrictions.
/// </summary>
public sealed class FolderOrganizationService : IFolderOrganizationService
{
    private readonly IFileOperationService _fileOperationService;
    private readonly IAiService _aiService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<FolderOrganizationService> _logger;

    /// <summary>
    /// Size thresholds in bytes for categorization.
    /// </summary>
    private static class SizeThresholds
    {
        public const long Tiny = 1024 * 1024;                    // 1 MB
        public const long Small = 10 * 1024 * 1024;              // 10 MB
        public const long Medium = 100 * 1024 * 1024;            // 100 MB
        public const long Large = 1024L * 1024 * 1024;           // 1 GB
    }

    public FolderOrganizationService(
        IFileOperationService fileOperationService,
        IAiService aiService,
        ISettingsService settingsService,
        ILogger<FolderOrganizationService> logger)
    {
        _fileOperationService = fileOperationService ?? throw new ArgumentNullException(nameof(fileOperationService));
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("FolderOrganizationService initialized");
    }

    /// <inheritdoc />
    public async Task<(bool IsValid, string? Error)> ValidateFolderAsync(
        string folderPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        if (!Directory.Exists(folderPath))
        {
            return (false, "The specified folder does not exist.");
        }

        try
        {
            // Check if we can read the folder
            var files = Directory.GetFiles(folderPath);
            if (files.Length == 0)
            {
                var subdirs = Directory.GetDirectories(folderPath);
                if (subdirs.Length == 0)
                {
                    return (false, "The folder is empty.");
                }
            }

            // Check if we have write access (needed to create subfolders)
            var testPath = Path.Combine(folderPath, $".autodrop_test_{Guid.NewGuid():N}");
            await Task.Run(() =>
            {
                Directory.CreateDirectory(testPath);
                Directory.Delete(testPath);
            }, cancellationToken).ConfigureAwait(false);

            return (true, null);
        }
        catch (UnauthorizedAccessException)
        {
            return (false, "You don't have permission to access this folder.");
        }
        catch (IOException ex)
        {
            return (false, $"Cannot access folder: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<int> GetFileCountAsync(
        string folderPath,
        bool includeSubdirectories,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        var searchOption = includeSubdirectories 
            ? SearchOption.AllDirectories 
            : SearchOption.TopDirectoryOnly;

        return await Task.Run(() =>
        {
            try
            {
                return Directory.GetFiles(folderPath, "*", searchOption).Length;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error counting files in {Folder}", folderPath);
                return 0;
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlannedFolderGroup>> PreviewOrganizationAsync(
        string folderPath,
        FolderOrganizationSettings settings,
        IProgress<FolderOrganizationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        ArgumentNullException.ThrowIfNull(settings);

        _logger.LogInformation("Previewing organization for {Folder} using criteria {Criteria}",
            folderPath, settings.Criteria);

        progress?.Report(new FolderOrganizationProgress
        {
            Phase = "Scanning",
            Status = "Scanning folder for files..."
        });

        // Get all files
        var files = await GetFilesAsync(folderPath, settings, cancellationToken).ConfigureAwait(false);

        if (files.Count == 0)
        {
            _logger.LogInformation("No files to organize in {Folder}", folderPath);
            return [];
        }

        // Enforce max files limit
        if (files.Count > settings.MaxFilesPerOperation)
        {
            _logger.LogWarning("Folder has {Count} files, limiting to {Max}",
                files.Count, settings.MaxFilesPerOperation);
            files = files.Take(settings.MaxFilesPerOperation).ToList();
        }

        _logger.LogDebug("Found {Count} files to organize", files.Count);

        // Group files based on criteria
        var plannedMoves = settings.Criteria switch
        {
            OrganizationCriteria.ByExtension => GroupByExtension(folderPath, files),
            OrganizationCriteria.ByCategory => GroupByCategory(folderPath, files),
            OrganizationCriteria.BySize => GroupBySize(folderPath, files),
            OrganizationCriteria.ByDate => GroupByDate(folderPath, files, settings),
            OrganizationCriteria.ByName => await GroupByNameAsync(
                folderPath, files, settings, progress, cancellationToken).ConfigureAwait(false),
            OrganizationCriteria.ByContent => await GroupByContentAsync(
                folderPath, files, settings, progress, cancellationToken).ConfigureAwait(false),
            _ => GroupByExtension(folderPath, files)
        };

        // Convert to groups
        var groups = plannedMoves
            .GroupBy(m => m.DestinationFolder)
            .Select(g => new PlannedFolderGroup
            {
                DestinationFolder = g.Key,
                GroupKey = g.First().Category,
                Files = g.ToList()
            })
            .OrderByDescending(g => g.FileCount)
            .ToList();

        _logger.LogInformation("Created {Count} groups for organization", groups.Count);

        return groups;
    }

    /// <inheritdoc />
    public async Task<FolderOrganizationResult> ExecuteOrganizationAsync(
        string folderPath,
        IEnumerable<PlannedFolderGroup> groups,
        IProgress<FolderOrganizationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        ArgumentNullException.ThrowIfNull(groups);

        var groupList = groups.ToList();
        
        // Count all files marked as skipped (files that have IsSkipped = true)
        var allFiles = groupList.SelectMany(g => g.Files).ToList();
        var skippedFiles = allFiles.Where(f => f.IsSkipped).ToList();
        var deselectedFiles = allFiles.Where(f => !f.IsSelected && !f.IsSkipped).ToList();
        
        var selectedFiles = allFiles
            .Where(f => f.IsSelected && !f.IsSkipped)
            .ToList();

        if (selectedFiles.Count == 0)
        {
            return FolderOrganizationResult.Failed("No files selected for organization.");
        }

        _logger.LogInformation("Executing organization for {Count} files ({Skipped} skipped, {Deselected} deselected)", 
            selectedFiles.Count, skippedFiles.Count, deselectedFiles.Count);

        var operations = new List<MoveOperation>();
        var errors = new List<FileOrganizationError>();
        var createdFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var movedCount = 0;
        var skippedCount = skippedFiles.Count; // Initialize with files that were pre-marked as skipped

        for (var i = 0; i < selectedFiles.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = selectedFiles[i];
            progress?.Report(new FolderOrganizationProgress
            {
                Phase = "Moving",
                CurrentFile = file.FileName,
                CurrentIndex = i + 1,
                TotalCount = selectedFiles.Count,
                Status = $"Moving to {file.DestinationFolderName}..."
            });

            try
            {
                // Create destination folder if needed
                if (!Directory.Exists(file.DestinationFolder))
                {
                    Directory.CreateDirectory(file.DestinationFolder);
                    createdFolders.Add(file.DestinationFolder);
                    _logger.LogDebug("Created folder: {Folder}", file.DestinationFolder);
                }

                // Move the file
                var result = await _fileOperationService
                    .MoveAsync(file.SourcePath, file.DestinationFolder, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                operations.Add(result);
                movedCount++;
                _logger.LogDebug("Moved {File} to {Destination}", file.FileName, file.DestinationFolder);
            }
            catch (Exception ex)
            {
                errors.Add(new FileOrganizationError
                {
                    FilePath = file.SourcePath,
                    Error = ex.Message
                });
                _logger.LogError(ex, "Error moving {File}", file.FileName);
            }
        }

        _logger.LogInformation("Organization complete: {Moved} moved, {Skipped} skipped, {Failed} failed",
            movedCount, skippedCount, errors.Count);

        return new FolderOrganizationResult
        {
            Success = errors.Count == 0,
            TotalFiles = selectedFiles.Count,
            MovedCount = movedCount,
            SkippedCount = skippedCount,
            FailedCount = errors.Count,
            FoldersCreated = createdFolders.Count,
            Operations = operations,
            Errors = errors
        };
    }

    /// <inheritdoc />
    public FileSizeRange GetSizeRange(long sizeInBytes)
    {
        return sizeInBytes switch
        {
            < SizeThresholds.Tiny => FileSizeRange.Tiny,
            < SizeThresholds.Small => FileSizeRange.Small,
            < SizeThresholds.Medium => FileSizeRange.Medium,
            < SizeThresholds.Large => FileSizeRange.Large,
            _ => FileSizeRange.Huge
        };
    }

    /// <inheritdoc />
    public string GetSizeRangeDisplayName(FileSizeRange range)
    {
        return range switch
        {
            FileSizeRange.Tiny => "Tiny (< 1 MB)",
            FileSizeRange.Small => "Small (1-10 MB)",
            FileSizeRange.Medium => "Medium (10-100 MB)",
            FileSizeRange.Large => "Large (100 MB - 1 GB)",
            FileSizeRange.Huge => "Huge (> 1 GB)",
            _ => "Unknown"
        };
    }

    #region Private Methods

    private async Task<List<FileInfo>> GetFilesAsync(
        string folderPath,
        FolderOrganizationSettings settings,
        CancellationToken cancellationToken)
    {
        var searchOption = settings.IncludeSubdirectories
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        return await Task.Run(() =>
        {
            var directory = new DirectoryInfo(folderPath);
            var files = directory.GetFiles("*", searchOption).AsEnumerable();

            // Filter hidden files
            if (settings.SkipHiddenFiles)
            {
                files = files.Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden));
            }

            // Filter excluded extensions
            if (settings.ExcludedExtensions.Count > 0)
            {
                var excluded = new HashSet<string>(
                    settings.ExcludedExtensions, 
                    StringComparer.OrdinalIgnoreCase);
                files = files.Where(f => !excluded.Contains(f.Extension));
            }

            return files.ToList();
        }, cancellationToken).ConfigureAwait(false);
    }

    private List<PlannedFileMove> GroupByExtension(string basePath, List<FileInfo> files)
    {
        return files.Select(f =>
        {
            var ext = string.IsNullOrEmpty(f.Extension) ? "_no_extension" : f.Extension.TrimStart('.').ToLowerInvariant();
            return new PlannedFileMove
            {
                SourcePath = f.FullName,
                DestinationFolder = Path.Combine(basePath, ext),
                Size = f.Length,
                Category = ext
            };
        }).ToList();
    }

    private List<PlannedFileMove> GroupByCategory(string basePath, List<FileInfo> files)
    {
        return files.Select(f =>
        {
            var category = FileCategories.GetCategory(f.Extension);
            return new PlannedFileMove
            {
                SourcePath = f.FullName,
                DestinationFolder = Path.Combine(basePath, category),
                Size = f.Length,
                Category = category
            };
        }).ToList();
    }

    private List<PlannedFileMove> GroupBySize(string basePath, List<FileInfo> files)
    {
        return files.Select(f =>
        {
            var range = GetSizeRange(f.Length);
            var folderName = range switch
            {
                FileSizeRange.Tiny => "Tiny_Under1MB",
                FileSizeRange.Small => "Small_1-10MB",
                FileSizeRange.Medium => "Medium_10-100MB",
                FileSizeRange.Large => "Large_100MB-1GB",
                FileSizeRange.Huge => "Huge_Over1GB",
                _ => "Unknown"
            };
            return new PlannedFileMove
            {
                SourcePath = f.FullName,
                DestinationFolder = Path.Combine(basePath, folderName),
                Size = f.Length,
                Category = GetSizeRangeDisplayName(range)
            };
        }).ToList();
    }

    private List<PlannedFileMove> GroupByDate(string basePath, List<FileInfo> files, FolderOrganizationSettings settings)
    {
        return files.Select(f =>
        {
            var date = settings.UseCreationDate ? f.CreationTime : f.LastWriteTime;
            var folderName = settings.DateFormat switch
            {
                DateOrganizationFormat.Year => date.ToString("yyyy"),
                DateOrganizationFormat.YearMonth => date.ToString("yyyy-MM"),
                DateOrganizationFormat.YearMonthDay => date.ToString("yyyy-MM-dd"),
                _ => date.ToString("yyyy-MM")
            };
            return new PlannedFileMove
            {
                SourcePath = f.FullName,
                DestinationFolder = Path.Combine(basePath, folderName),
                Size = f.Length,
                Category = folderName
            };
        }).ToList();
    }

    #region AI-Powered Organization

    private const string NameCategorizationPrompt = """
        You are a file organization assistant. Analyze the filename and suggest the best folder name to organize it.

        FILENAME: {0}
        EXTENSION: {1}

        Based on the filename pattern, suggest ONE folder name. Consider:
        - Common prefixes: IMG_, DSC_, VID_, Screenshot, Invoice, Report, Contract, Resume, etc.
        - Date patterns in name (2024-01-15, 20240115)
        - Project names or identifiers
        - Document types (receipt, statement, certificate, etc.)
        - Source apps (WhatsApp, Telegram, Chrome, etc.)

        Rules:
        1. Return ONLY the folder name, nothing else
        2. Use PascalCase or underscores (e.g., "Screenshots", "Work_Projects")
        3. Keep it short (1-3 words max)
        4. Be consistent: similar files should get same folder
        5. Common groupings: Photos, Screenshots, Videos, Documents, Invoices, Receipts, Reports, Downloads, Projects, Personal, Work

        Respond with ONLY the folder name.
        """;

    private const string ContentCategorizationPrompt = """
        You are a file organization assistant. Analyze this file's content and suggest the best folder name.

        FILENAME: {0}
        FILE TYPE: {1}

        Based on what you see in the content, suggest ONE folder name that describes what this file is about.

        Rules:
        1. Return ONLY the folder name, nothing else
        2. Use PascalCase or underscores (e.g., "Travel_Photos", "Work_Documents")
        3. Keep it short (1-3 words max)
        4. Focus on the CONTENT/SUBJECT, not the file type
        5. Common content categories: 
           - For images: Travel, Family, Food, Nature, Screenshots, Memes, Products, Receipts
           - For documents: Invoices, Contracts, Reports, Notes, Resumes, Certificates, Statements

        Respond with ONLY the folder name.
        """;

    private async Task<List<PlannedFileMove>> GroupByNameAsync(
        string basePath,
        List<FileInfo> files,
        FolderOrganizationSettings settings,
        IProgress<FolderOrganizationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var results = new List<PlannedFileMove>();

        // Check AI availability (async to refresh cache if needed)
        var isAiAvailable = await _aiService.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
        
        // Local AI uses embeddings and can't generate text responses for filename analysis
        // Fall back to pattern detection if AI is unavailable or doesn't support text prompts
        if (!isAiAvailable || !_aiService.SupportsTextPrompts)
        {
            _logger.LogWarning("AI service not available or doesn't support text prompts (provider: {Provider}), using fallback pattern detection",
                _aiService.ActiveProvider);
            return GroupByNameFallback(basePath, files);
        }

        // Limit files for AI processing
        var filesToProcess = files.Take(settings.MaxAiFilesPerOperation).ToList();
        var skippedFiles = files.Skip(settings.MaxAiFilesPerOperation).ToList();

        _logger.LogInformation("AI name analysis: {Process} files to analyze, {Skipped} skipped",
            filesToProcess.Count, skippedFiles.Count);

        // Process files with AI
        for (var i = 0; i < filesToProcess.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = filesToProcess[i];
            progress?.Report(new FolderOrganizationProgress
            {
                Phase = "Analyzing",
                CurrentFile = file.Name,
                CurrentIndex = i + 1,
                TotalCount = filesToProcess.Count,
                Status = "Analyzing filename..."
            });

            try
            {
                var fileName = Path.GetFileNameWithoutExtension(file.Name);
                var prompt = string.Format(NameCategorizationPrompt, fileName, file.Extension);

                var response = await _aiService.AnalyzeTextAsync(prompt, cancellationToken).ConfigureAwait(false);
                var folderName = SanitizeFolderName(response);

                _logger.LogDebug("AI categorized filename {File} as {Folder}", file.Name, folderName);

                results.Add(new PlannedFileMove
                {
                    SourcePath = file.FullName,
                    DestinationFolder = Path.Combine(basePath, folderName),
                    Size = file.Length,
                    Category = folderName,
                    AiCategory = folderName,
                    AiConfidence = null // Name-based analysis doesn't provide real confidence
                });

                // Rate limiting
                if (i < filesToProcess.Count - 1)
                {
                    await Task.Delay(settings.AiDelayMs, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI name analysis failed for {File}, using fallback", file.Name);
                var fallbackFolder = DetectNamePatternFallback(Path.GetFileNameWithoutExtension(file.Name));
                results.Add(new PlannedFileMove
                {
                    SourcePath = file.FullName,
                    DestinationFolder = Path.Combine(basePath, fallbackFolder),
                    Size = file.Length,
                    Category = fallbackFolder
                });
            }
        }

        // Add skipped files with fallback categorization
        foreach (var file in skippedFiles)
        {
            var fallbackFolder = DetectNamePatternFallback(Path.GetFileNameWithoutExtension(file.Name));
            results.Add(new PlannedFileMove
            {
                SourcePath = file.FullName,
                DestinationFolder = Path.Combine(basePath, fallbackFolder),
                Size = file.Length,
                Category = fallbackFolder,
                SkipReason = "Rate limit - using pattern detection"
            });
        }

        return results;
    }

    private List<PlannedFileMove> GroupByNameFallback(string basePath, List<FileInfo> files)
    {
        return files.Select(f =>
        {
            var fileName = Path.GetFileNameWithoutExtension(f.Name);
            var folderName = DetectNamePatternFallback(fileName);

            return new PlannedFileMove
            {
                SourcePath = f.FullName,
                DestinationFolder = Path.Combine(basePath, folderName),
                Size = f.Length,
                Category = folderName
            };
        }).ToList();
    }

    /// <summary>
    /// Fallback pattern detection when AI is unavailable.
    /// </summary>
    private static string DetectNamePatternFallback(string fileName)
    {
        // Common prefix patterns
        var patterns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "IMG", "Photos" }, { "DSC", "Photos" }, { "Photo", "Photos" },
            { "Screenshot", "Screenshots" }, { "Screen", "Screenshots" }, { "Capture", "Screenshots" },
            { "VID", "Videos" }, { "MOV", "Videos" }, { "Video", "Videos" },
            { "Invoice", "Invoices" }, { "Receipt", "Receipts" }, { "Report", "Reports" },
            { "Contract", "Contracts" }, { "Resume", "Resumes" }, { "CV", "Resumes" },
            { "WhatsApp", "WhatsApp" }, { "Telegram", "Telegram" },
            { "Scan", "Scans" }, { "Document", "Documents" }, { "Doc", "Documents" }
        };

        foreach (var (prefix, folder) in patterns)
        {
            if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return folder;
        }

        // Date pattern
        if (System.Text.RegularExpressions.Regex.IsMatch(fileName, @"^\d{4}[-_]?\d{2}[-_]?\d{2}"))
            return "Date_Named";

        // Numeric prefix
        if (System.Text.RegularExpressions.Regex.IsMatch(fileName, @"^\d{2,4}[_\-]"))
            return "Numbered";

        // First letter grouping
        if (!string.IsNullOrEmpty(fileName) && char.IsLetter(fileName[0]))
            return $"_{char.ToUpper(fileName[0])}";

        return "_Other";
    }

    #endregion

    private async Task<List<PlannedFileMove>> GroupByContentAsync(
        string basePath,
        List<FileInfo> files,
        FolderOrganizationSettings settings,
        IProgress<FolderOrganizationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var results = new List<PlannedFileMove>();
        var aiAnalyzableFiles = new List<FileInfo>();
        var nonAiFiles = new List<FileInfo>();

        // First pass: Separate AI-analyzable from non-analyzable files
        foreach (var file in files)
        {
            var sizeMb = file.Length / (1024.0 * 1024.0);
            var ext = file.Extension.ToLowerInvariant();

            // Check if file is AI-analyzable (images and documents)
            var isImage = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" }
                .Contains(ext, StringComparer.OrdinalIgnoreCase);
            var isDocument = new[] { ".txt", ".md", ".json", ".xml", ".csv", ".log", ".pdf" }
                .Contains(ext, StringComparer.OrdinalIgnoreCase);

            if ((isImage || isDocument) && sizeMb <= settings.MaxAiFileSizeMb)
            {
                aiAnalyzableFiles.Add(file);
            }
            else
            {
                nonAiFiles.Add(file);
            }
        }

        // Enforce rate limiting on AI files
        var filesToAnalyze = aiAnalyzableFiles.Take(settings.MaxAiFilesPerOperation).ToList();
        var skippedAiFiles = aiAnalyzableFiles.Skip(settings.MaxAiFilesPerOperation).ToList();

        _logger.LogInformation("AI analysis: {Analyze} files to analyze, {Skipped} skipped due to rate limit, {NonAi} non-AI files",
            filesToAnalyze.Count, skippedAiFiles.Count, nonAiFiles.Count);

        // Check if AI is available (async to refresh cache)
        var isAiAvailable = await _aiService.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
        if (!isAiAvailable)
        {
            _logger.LogWarning("AI service not available, falling back to category-based organization");
            // Fallback to category-based for all files
            return GroupByCategory(basePath, files);
        }

        // Get custom folders for AI matching
        var appSettings = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        var customFolders = appSettings.CustomFolders;

        // Analyze AI-compatible files with rate limiting
        for (var i = 0; i < filesToAnalyze.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = filesToAnalyze[i];
            progress?.Report(new FolderOrganizationProgress
            {
                Phase = "AI Analysis",
                CurrentFile = file.Name,
                CurrentIndex = i + 1,
                TotalCount = filesToAnalyze.Count,
                Status = $"Analyzing {file.Name}..."
            });

            try
            {
                var aiResult = await _aiService
                    .AnalyzeFileAsync(file.FullName, customFolders, cancellationToken)
                    .ConfigureAwait(false);

                string destinationFolder;
                string category;
                double? confidence = null;

                if (aiResult.Success && !string.IsNullOrEmpty(aiResult.Category))
                {
                    // Use AI-suggested category
                    category = aiResult.Category;
                    confidence = aiResult.Confidence;

                    // Check if AI matched to a custom folder
                    if (aiResult.HasMatchedFolder && !string.IsNullOrEmpty(aiResult.MatchedFolderPath))
                    {
                        destinationFolder = aiResult.MatchedFolderPath;
                    }
                    else
                    {
                        // Create subfolder in base path with AI category name
                        destinationFolder = Path.Combine(basePath, SanitizeFolderName(category));
                    }

                    _logger.LogDebug("AI categorized {File} as {Category} (confidence: {Confidence:P0})",
                        file.Name, category, confidence);
                }
                else
                {
                    // Fallback to file category
                    category = FileCategories.GetCategory(file.Extension);
                    destinationFolder = Path.Combine(basePath, category);
                    _logger.LogDebug("AI fallback for {File}: {Category}", file.Name, category);
                }

                results.Add(new PlannedFileMove
                {
                    SourcePath = file.FullName,
                    DestinationFolder = destinationFolder,
                    Size = file.Length,
                    Category = category,
                    AiConfidence = confidence,
                    AiCategory = aiResult.Success ? aiResult.Category : null
                });

                // Rate limiting delay between API calls
                if (i < filesToAnalyze.Count - 1 && settings.AiDelayMs > 0)
                {
                    await Task.Delay(settings.AiDelayMs, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI analysis failed for {File}, using fallback", file.Name);
                var category = FileCategories.GetCategory(file.Extension);
                results.Add(new PlannedFileMove
                {
                    SourcePath = file.FullName,
                    DestinationFolder = Path.Combine(basePath, category),
                    Size = file.Length,
                    Category = category,
                    SkipReason = $"AI analysis failed: {ex.Message}"
                });
            }
        }

        // Add skipped AI files (due to rate limit) with notice
        foreach (var file in skippedAiFiles)
        {
            var category = FileCategories.GetCategory(file.Extension);
            results.Add(new PlannedFileMove
            {
                SourcePath = file.FullName,
                DestinationFolder = Path.Combine(basePath, category),
                Size = file.Length,
                Category = category,
                SkipReason = $"Skipped AI analysis (rate limit: max {settings.MaxAiFilesPerOperation} files)"
            });
        }

        // Add non-AI-analyzable files using category fallback
        foreach (var file in nonAiFiles)
        {
            var category = FileCategories.GetCategory(file.Extension);
            var skipReason = file.Length > settings.MaxAiFileSizeMb * 1024 * 1024
                ? $"File too large for AI ({file.Length / (1024 * 1024):F1} MB > {settings.MaxAiFileSizeMb} MB)"
                : "File type not supported for AI analysis";

            results.Add(new PlannedFileMove
            {
                SourcePath = file.FullName,
                DestinationFolder = Path.Combine(basePath, category),
                Size = file.Length,
                Category = category,
                SkipReason = skipReason
            });
        }

        return results;
    }

    private static string SanitizeFolderName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Other";

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        
        // Trim leading/trailing whitespace and dots (Windows doesn't allow trailing dots/spaces)
        sanitized = sanitized.Trim().TrimEnd('.');
        
        if (string.IsNullOrWhiteSpace(sanitized))
            return "Other";
        
        // Windows reserved device names that cannot be used as folder names
        var reservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };
        
        // Check if the name (without extension) is a reserved name
        var nameWithoutExt = Path.GetFileNameWithoutExtension(sanitized);
        if (reservedNames.Contains(nameWithoutExt))
        {
            sanitized = $"_{sanitized}";
        }
        
        return sanitized;
    }

    #endregion
}
