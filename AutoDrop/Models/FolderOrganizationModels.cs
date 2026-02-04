using System.Text.Json.Serialization;

namespace AutoDrop.Models;

/// <summary>
/// Criteria for organizing files within a folder.
/// </summary>
public enum OrganizationCriteria
{
    /// <summary>
    /// Organize files by their extension (.pdf, .jpg, etc.).
    /// </summary>
    ByExtension,

    /// <summary>
    /// Organize files by their file type category (Documents, Images, Videos, etc.).
    /// </summary>
    ByCategory,

    /// <summary>
    /// Organize files by file size ranges (Small, Medium, Large).
    /// </summary>
    BySize,

    /// <summary>
    /// Organize files by creation/modification date.
    /// </summary>
    ByDate,

    /// <summary>
    /// Organize files by common filename patterns (IMG_, Screenshot_, Invoice_, etc.).
    /// </summary>
    ByName,

    /// <summary>
    /// Organize files using AI content analysis.
    /// </summary>
    ByContent
}

/// <summary>
/// Size range for organizing files by size.
/// </summary>
public enum FileSizeRange
{
    /// <summary>
    /// Files under 1 MB.
    /// </summary>
    Tiny,

    /// <summary>
    /// Files between 1 MB and 10 MB.
    /// </summary>
    Small,

    /// <summary>
    /// Files between 10 MB and 100 MB.
    /// </summary>
    Medium,

    /// <summary>
    /// Files between 100 MB and 1 GB.
    /// </summary>
    Large,

    /// <summary>
    /// Files over 1 GB.
    /// </summary>
    Huge
}

/// <summary>
/// Date range for organizing files by date.
/// </summary>
public enum DateOrganizationFormat
{
    /// <summary>
    /// Organize by year (e.g., 2026).
    /// </summary>
    Year,

    /// <summary>
    /// Organize by year and month (e.g., 2026-01).
    /// </summary>
    YearMonth,

    /// <summary>
    /// Organize by year, month, and day (e.g., 2026-01-15).
    /// </summary>
    YearMonthDay
}

/// <summary>
/// Settings for organizing files within a folder.
/// </summary>
public sealed class FolderOrganizationSettings
{
    /// <summary>
    /// The primary criteria for organizing files.
    /// </summary>
    [JsonPropertyName("criteria")]
    public OrganizationCriteria Criteria { get; set; } = OrganizationCriteria.ByExtension;

    /// <summary>
    /// Whether to include files in subdirectories (recursive).
    /// </summary>
    [JsonPropertyName("includeSubdirectories")]
    public bool IncludeSubdirectories { get; set; }

    /// <summary>
    /// Date format when organizing by date.
    /// </summary>
    [JsonPropertyName("dateFormat")]
    public DateOrganizationFormat DateFormat { get; set; } = DateOrganizationFormat.YearMonth;

    /// <summary>
    /// Whether to use creation date (true) or modification date (false) for date organization.
    /// </summary>
    [JsonPropertyName("useCreationDate")]
    public bool UseCreationDate { get; set; }

    /// <summary>
    /// Maximum file size in MB for AI analysis (prevents timeouts).
    /// </summary>
    [JsonPropertyName("maxAiFileSizeMb")]
    public int MaxAiFileSizeMb { get; set; } = 10;

    /// <summary>
    /// Maximum number of files to analyze with AI per operation (rate limiting).
    /// </summary>
    [JsonPropertyName("maxAiFilesPerOperation")]
    public int MaxAiFilesPerOperation { get; set; } = 20;

    /// <summary>
    /// Delay in milliseconds between AI API calls (rate limiting).
    /// </summary>
    [JsonPropertyName("aiDelayMs")]
    public int AiDelayMs { get; set; } = 500;

    /// <summary>
    /// Whether to create subfolders for size ranges.
    /// </summary>
    [JsonPropertyName("createSizeSubfolders")]
    public bool CreateSizeSubfolders { get; set; } = true;

    /// <summary>
    /// Whether to move files (true) or just preview the organization (false).
    /// </summary>
    [JsonPropertyName("performMove")]
    public bool PerformMove { get; set; } = true;

    /// <summary>
    /// Custom folder name prefix for organized subfolders.
    /// </summary>
    [JsonPropertyName("folderNamePrefix")]
    public string FolderNamePrefix { get; set; } = string.Empty;

    /// <summary>
    /// Whether to skip hidden files.
    /// </summary>
    [JsonPropertyName("skipHiddenFiles")]
    public bool SkipHiddenFiles { get; set; } = true;

    /// <summary>
    /// File extensions to exclude from organization.
    /// </summary>
    [JsonPropertyName("excludedExtensions")]
    public List<string> ExcludedExtensions { get; set; } = [];

    /// <summary>
    /// Maximum total files to process in one operation.
    /// </summary>
    [JsonPropertyName("maxFilesPerOperation")]
    public int MaxFilesPerOperation { get; set; } = 500;
}

/// <summary>
/// Represents a planned file move during folder organization preview.
/// </summary>
public sealed class PlannedFileMove
{
    /// <summary>
    /// Source file path.
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// Source file name.
    /// </summary>
    public string FileName => Path.GetFileName(SourcePath);

    /// <summary>
    /// Planned destination folder path.
    /// </summary>
    public required string DestinationFolder { get; init; }

    /// <summary>
    /// Destination folder name for display.
    /// </summary>
    public string DestinationFolderName => Path.GetFileName(DestinationFolder.TrimEnd(Path.DirectorySeparatorChar));

    /// <summary>
    /// Full destination path including filename.
    /// </summary>
    public string DestinationPath => Path.Combine(DestinationFolder, FileName);

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// File extension.
    /// </summary>
    public string Extension => Path.GetExtension(SourcePath);

    /// <summary>
    /// File category (Image, Document, etc.).
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Whether this file is selected for moving.
    /// </summary>
    public bool IsSelected { get; set; } = true;

    /// <summary>
    /// AI analysis confidence (if organized by AI content).
    /// </summary>
    public double? AiConfidence { get; init; }

    /// <summary>
    /// AI suggested category (if organized by AI content).
    /// </summary>
    public string? AiCategory { get; init; }

    /// <summary>
    /// Reason for skipping this file (if any).
    /// </summary>
    public string? SkipReason { get; init; }

    /// <summary>
    /// Whether this file was skipped.
    /// </summary>
    public bool IsSkipped => !string.IsNullOrEmpty(SkipReason);
}

/// <summary>
/// Group of files planned for the same destination folder.
/// </summary>
public sealed class PlannedFolderGroup
{
    /// <summary>
    /// Destination folder path.
    /// </summary>
    public required string DestinationFolder { get; init; }

    /// <summary>
    /// Display name for the destination folder.
    /// </summary>
    public string FolderName => Path.GetFileName(DestinationFolder.TrimEnd(Path.DirectorySeparatorChar));

    /// <summary>
    /// Category or criteria value for this group (e.g., ".pdf", "Documents", "Large").
    /// </summary>
    public required string GroupKey { get; init; }

    /// <summary>
    /// Files in this group.
    /// </summary>
    public List<PlannedFileMove> Files { get; init; } = [];

    /// <summary>
    /// Number of files in this group.
    /// </summary>
    public int FileCount => Files.Count;

    /// <summary>
    /// Number of selected files in this group.
    /// </summary>
    public int SelectedCount => Files.Count(f => f.IsSelected);

    /// <summary>
    /// Total size of files in this group.
    /// </summary>
    public long TotalSize => Files.Sum(f => f.Size);

    /// <summary>
    /// Whether all files in this group are selected.
    /// </summary>
    public bool IsAllSelected
    {
        get => Files.All(f => f.IsSelected);
        set
        {
            foreach (var file in Files)
            {
                file.IsSelected = value;
            }
        }
    }

    /// <summary>
    /// Whether the destination folder already exists.
    /// </summary>
    public bool FolderExists => Directory.Exists(DestinationFolder);
}

/// <summary>
/// Result of a folder organization operation.
/// </summary>
public sealed class FolderOrganizationResult
{
    /// <summary>
    /// Whether the organization was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Total number of files processed.
    /// </summary>
    public int TotalFiles { get; init; }

    /// <summary>
    /// Number of files successfully moved.
    /// </summary>
    public int MovedCount { get; init; }

    /// <summary>
    /// Number of files skipped.
    /// </summary>
    public int SkippedCount { get; init; }

    /// <summary>
    /// Number of files that failed to move.
    /// </summary>
    public int FailedCount { get; init; }

    /// <summary>
    /// Number of folders created.
    /// </summary>
    public int FoldersCreated { get; init; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// List of move operations performed (for undo support).
    /// </summary>
    public List<MoveOperation> Operations { get; init; } = [];

    /// <summary>
    /// List of errors for individual files.
    /// </summary>
    public List<FileOrganizationError> Errors { get; init; } = [];

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static FolderOrganizationResult Failed(string error) => new()
    {
        Success = false,
        ErrorMessage = error
    };

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static FolderOrganizationResult Successful(int totalFiles, int movedCount, int skippedCount, int foldersCreated, List<MoveOperation> operations) => new()
    {
        Success = true,
        TotalFiles = totalFiles,
        MovedCount = movedCount,
        SkippedCount = skippedCount,
        FoldersCreated = foldersCreated,
        Operations = operations
    };
}

/// <summary>
/// Error during file organization.
/// </summary>
public sealed class FileOrganizationError
{
    /// <summary>
    /// File that failed.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Error message.
    /// </summary>
    public required string Error { get; init; }
}

/// <summary>
/// Progress report for folder organization.
/// </summary>
public sealed class FolderOrganizationProgress
{
    /// <summary>
    /// Current phase of the operation.
    /// </summary>
    public required string Phase { get; init; }

    /// <summary>
    /// Current file being processed.
    /// </summary>
    public string CurrentFile { get; init; } = string.Empty;

    /// <summary>
    /// Current index (1-based).
    /// </summary>
    public int CurrentIndex { get; init; }

    /// <summary>
    /// Total count.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public int ProgressPercent => TotalCount > 0 ? (CurrentIndex * 100) / TotalCount : 0;

    /// <summary>
    /// Status message.
    /// </summary>
    public string Status { get; init; } = string.Empty;
}
