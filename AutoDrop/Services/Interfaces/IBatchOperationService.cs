using AutoDrop.Models;

namespace AutoDrop.Services.Interfaces;

/// <summary>
/// Service for handling batch file operations with grouping and progress reporting.
/// </summary>
public interface IBatchOperationService
{
    /// <summary>
    /// Groups dropped items by their category and suggested destination.
    /// </summary>
    /// <param name="items">Items to group.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Groups of items with their suggested destinations.</returns>
    Task<IReadOnlyList<BatchFileGroup>> GroupItemsByDestinationAsync(
        IEnumerable<DroppedItem> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a batch move operation for the selected groups.
    /// </summary>
    /// <param name="groups">Groups to process.</param>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="duplicateHandling">How to handle duplicates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the batch operation.</returns>
    Task<BatchOperationResult> ExecuteBatchMoveAsync(
        IEnumerable<BatchFileGroup> groups,
        IProgress<BatchProgressReport>? progress = null,
        DuplicateHandling duplicateHandling = DuplicateHandling.KeepBothAll,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Undoes a batch operation.
    /// </summary>
    /// <param name="operations">Operations to undo.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of successfully undone operations.</returns>
    Task<int> UndoBatchAsync(
        IEnumerable<MoveOperation> operations,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when a duplicate is detected and user decision is needed.
    /// </summary>
    event EventHandler<DuplicateDetectedEventArgs>? DuplicateDetected;
}

/// <summary>
/// Progress report for batch operations.
/// </summary>
public sealed class BatchProgressReport
{
    /// <summary>
    /// Current item being processed.
    /// </summary>
    public string CurrentItem { get; init; } = string.Empty;

    /// <summary>
    /// Current item index (1-based).
    /// </summary>
    public int CurrentIndex { get; init; }

    /// <summary>
    /// Total number of items.
    /// </summary>
    public int TotalItems { get; init; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public int ProgressPercent => TotalItems > 0 ? (CurrentIndex * 100) / TotalItems : 0;

    /// <summary>
    /// Current operation status.
    /// </summary>
    public string Status { get; init; } = string.Empty;
}

/// <summary>
/// Event args for duplicate detection during batch operations.
/// </summary>
public sealed class DuplicateDetectedEventArgs : EventArgs
{
    /// <summary>
    /// Source file information.
    /// </summary>
    public FileComparisonInfo SourceFile { get; init; } = new();

    /// <summary>
    /// Destination file information.
    /// </summary>
    public FileComparisonInfo DestinationFile { get; init; } = new();

    /// <summary>
    /// Whether the files are exact duplicates (same content).
    /// </summary>
    public bool IsExactMatch { get; init; }

    /// <summary>
    /// User's decision on how to handle this duplicate.
    /// </summary>
    public DuplicateHandling UserDecision { get; set; } = DuplicateHandling.Ask;

    /// <summary>
    /// Whether to apply this decision to all remaining duplicates.
    /// </summary>
    public bool ApplyToAll { get; set; }
}
