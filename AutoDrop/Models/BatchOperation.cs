using System.Text.Json.Serialization;

namespace AutoDrop.Models;

/// <summary>
/// Represents a group of files with the same extension/destination for batch operations.
/// </summary>
public sealed class BatchFileGroup
{
    /// <summary>
    /// Category of files in this group (e.g., "Document", "Image").
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// File extension for this group (e.g., ".png", ".jpg").
    /// </summary>
    public string Extension { get; init; } = string.Empty;

    /// <summary>
    /// Target destination path for this group.
    /// </summary>
    public string DestinationPath { get; init; } = string.Empty;

    /// <summary>
    /// Display name for the destination (folder name).
    /// </summary>
    public string DestinationDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Files in this group.
    /// </summary>
    public List<DroppedItem> Items { get; init; } = [];

    /// <summary>
    /// Whether this group is selected for the batch operation.
    /// </summary>
    public bool IsSelected { get; set; } = true;

    /// <summary>
    /// Number of files in this group.
    /// </summary>
    public int FileCount => Items.Count;

    /// <summary>
    /// Total size of files in this group.
    /// </summary>
    public long TotalSize => Items.Sum(i => i.Size);

    /// <summary>
    /// Formatted display text for this group (e.g., "5 PNGs", "3 JPGs").
    /// </summary>
    public string DisplayText
    {
        get
        {
            if (Items.Count == 0) return string.Empty;
            
            // Use the Extension property which is set during grouping
            var ext = Extension.TrimStart('.').ToUpperInvariant();
            if (string.IsNullOrEmpty(ext) || ext == "FOLDER")
            {
                return Items.Count == 1 ? "1 folder" : $"{Items.Count} folders";
            }
            
            return Items.Count == 1 
                ? $"1 {ext}" 
                : $"{Items.Count} {ext}s";
        }
    }
}

/// <summary>
/// Result of a batch move operation.
/// </summary>
public sealed class BatchOperationResult
{
    /// <summary>
    /// Total number of items processed.
    /// </summary>
    public int TotalItems { get; init; }

    /// <summary>
    /// Number of successfully moved items.
    /// </summary>
    public int SuccessCount { get; init; }

    /// <summary>
    /// Number of failed items.
    /// </summary>
    public int FailedCount { get; init; }

    /// <summary>
    /// Number of skipped items (duplicates, user cancelled, etc.).
    /// </summary>
    public int SkippedCount { get; init; }

    /// <summary>
    /// Number of unique destination folders used.
    /// </summary>
    public int DestinationCount { get; init; }

    /// <summary>
    /// Errors that occurred during the operation.
    /// </summary>
    public List<BatchOperationError> Errors { get; init; } = [];

    /// <summary>
    /// Operations that were performed (for undo support).
    /// </summary>
    public List<MoveOperation> Operations { get; init; } = [];

    /// <summary>
    /// Whether all items were processed successfully.
    /// </summary>
    public bool IsFullSuccess => SuccessCount == TotalItems && FailedCount == 0;

    /// <summary>
    /// Whether all items failed.
    /// </summary>
    public bool IsFullFailure => FailedCount == TotalItems && SuccessCount == 0;

    /// <summary>
    /// Creates a summary message for the operation result.
    /// </summary>
    public string GetSummaryMessage()
    {
        if (IsFullSuccess)
        {
            return DestinationCount == 1
                ? $"Organized {SuccessCount} file{(SuccessCount == 1 ? "" : "s")} successfully"
                : $"Organized {SuccessCount} file{(SuccessCount == 1 ? "" : "s")} to {DestinationCount} folders";
        }

        if (IsFullFailure)
        {
            return $"Failed to organize {FailedCount} file{(FailedCount == 1 ? "" : "s")}";
        }

        var parts = new List<string>();
        if (SuccessCount > 0) parts.Add($"{SuccessCount} succeeded");
        if (FailedCount > 0) parts.Add($"{FailedCount} failed");
        if (SkippedCount > 0) parts.Add($"{SkippedCount} skipped");

        return string.Join(", ", parts);
    }
}

/// <summary>
/// Error that occurred during a batch operation.
/// </summary>
public sealed class BatchOperationError
{
    /// <summary>
    /// Item that failed.
    /// </summary>
    public string ItemName { get; init; } = string.Empty;

    /// <summary>
    /// Error message.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Source path of the failed item.
    /// </summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>
    /// Destination path that was attempted.
    /// </summary>
    public string DestinationPath { get; init; } = string.Empty;
}

/// <summary>
/// Options for handling duplicates in batch operations.
/// </summary>
public enum DuplicateHandling
{
    /// <summary>
    /// Prompt user for each duplicate.
    /// </summary>
    Ask,

    /// <summary>
    /// Skip all duplicates.
    /// </summary>
    SkipAll,

    /// <summary>
    /// Replace all duplicates.
    /// </summary>
    ReplaceAll,

    /// <summary>
    /// Keep both (rename new file).
    /// </summary>
    KeepBothAll,

    /// <summary>
    /// Delete source file (destination already has same content).
    /// </summary>
    DeleteSourceAll
}
