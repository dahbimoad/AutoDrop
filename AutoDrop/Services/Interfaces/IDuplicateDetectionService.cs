namespace AutoDrop.Services.Interfaces;

/// <summary>
/// Service for detecting duplicate files based on content hash comparison.
/// </summary>
public interface IDuplicateDetectionService
{
    /// <summary>
    /// Checks if a file at the destination is a duplicate of the source file.
    /// </summary>
    /// <param name="sourceFilePath">Path to the source file.</param>
    /// <param name="destinationFilePath">Path to check for duplicate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Duplicate check result with details.</returns>
    Task<DuplicateCheckResult> CheckForDuplicateAsync(
        string sourceFilePath, 
        string destinationFilePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the SHA256 hash of a file for duplicate detection.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Hex string of the SHA256 hash.</returns>
    Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the maximum file size (in bytes) for hash-based comparison.
    /// Files larger than this will use size/date comparison only.
    /// </summary>
    long MaxHashFileSizeBytes { get; }

    /// <summary>
    /// Gets or sets whether duplicate detection is enabled.
    /// </summary>
    bool IsEnabled { get; set; }
}

/// <summary>
/// Result of a duplicate file check.
/// </summary>
public sealed class DuplicateCheckResult
{
    /// <summary>
    /// Whether a duplicate was detected.
    /// </summary>
    public bool IsDuplicate { get; init; }

    /// <summary>
    /// Whether the files have identical content (hash match).
    /// </summary>
    public bool IsExactMatch { get; init; }

    /// <summary>
    /// The comparison method used.
    /// </summary>
    public DuplicateComparisonMethod ComparisonMethod { get; init; }

    /// <summary>
    /// Source file information.
    /// </summary>
    public FileComparisonInfo Source { get; init; } = new();

    /// <summary>
    /// Destination file information.
    /// </summary>
    public FileComparisonInfo Destination { get; init; } = new();

    /// <summary>
    /// Error message if comparison failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Whether the check completed successfully.
    /// </summary>
    public bool Success => string.IsNullOrEmpty(ErrorMessage);

    /// <summary>
    /// Creates a result indicating no duplicate exists.
    /// </summary>
    public static DuplicateCheckResult NoDuplicate() => new()
    {
        IsDuplicate = false,
        IsExactMatch = false,
        ComparisonMethod = DuplicateComparisonMethod.None
    };

    /// <summary>
    /// Creates a result indicating an error occurred.
    /// </summary>
    public static DuplicateCheckResult Error(string message) => new()
    {
        IsDuplicate = false,
        IsExactMatch = false,
        ComparisonMethod = DuplicateComparisonMethod.None,
        ErrorMessage = message
    };
}

/// <summary>
/// Information about a file being compared.
/// </summary>
public sealed record FileComparisonInfo
{
    /// <summary>
    /// Full file path.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// Last modified date.
    /// </summary>
    public DateTime LastModified { get; init; }

    /// <summary>
    /// SHA256 hash if computed.
    /// </summary>
    public string? Hash { get; init; }
}

/// <summary>
/// Method used to compare files for duplicates.
/// </summary>
public enum DuplicateComparisonMethod
{
    /// <summary>
    /// No comparison performed.
    /// </summary>
    None,

    /// <summary>
    /// Compared only file size.
    /// </summary>
    SizeOnly,

    /// <summary>
    /// Compared size and last modified date.
    /// </summary>
    SizeAndDate,

    /// <summary>
    /// Compared using SHA256 hash.
    /// </summary>
    Hash
}
