using AutoDrop.Models;

namespace AutoDrop.Services.Interfaces;

/// <summary>
/// Service responsible for tracking and managing file operation history.
/// Every file operation is recorded with full rollback capability.
/// </summary>
public interface IHistoryService
{
    /// <summary>
    /// Event raised when the history changes.
    /// </summary>
    event EventHandler? HistoryChanged;

    /// <summary>
    /// Gets the total count of operations in history.
    /// </summary>
    int TotalCount { get; }

    /// <summary>
    /// Gets the count of operations that can be undone.
    /// </summary>
    int UndoableCount { get; }

    /// <summary>
    /// Adds a new operation to the history.
    /// </summary>
    /// <param name="item">The operation to record.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddOperationAsync(OperationHistoryItem item, CancellationToken ct = default);

    /// <summary>
    /// Creates and adds an operation from file paths.
    /// </summary>
    /// <param name="sourcePath">Original file path.</param>
    /// <param name="destinationPath">Destination file path.</param>
    /// <param name="operationType">Type of operation.</param>
    /// <param name="aiConfidence">Optional AI confidence score.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created history item.</returns>
    Task<OperationHistoryItem> RecordOperationAsync(
        string sourcePath, 
        string destinationPath, 
        OperationType operationType = OperationType.Move,
        double? aiConfidence = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all operations from history.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of all history items, newest first.</returns>
    Task<IReadOnlyList<OperationHistoryItem>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets recent operations from history.
    /// </summary>
    /// <param name="count">Maximum number of items to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of recent history items, newest first.</returns>
    Task<IReadOnlyList<OperationHistoryItem>> GetRecentAsync(int count = 20, CancellationToken ct = default);

    /// <summary>
    /// Gets operations that can still be undone.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of undoable operations.</returns>
    Task<IReadOnlyList<OperationHistoryItem>> GetUndoableAsync(CancellationToken ct = default);

    /// <summary>
    /// Undoes a specific operation by ID.
    /// </summary>
    /// <param name="operationId">The operation ID to undo.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if undo succeeded, false otherwise.</returns>
    Task<bool> UndoOperationAsync(Guid operationId, CancellationToken ct = default);

    /// <summary>
    /// Undoes multiple operations.
    /// </summary>
    /// <param name="operationIds">The operation IDs to undo.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of (succeeded count, failed count).</returns>
    Task<(int succeeded, int failed)> UndoMultipleAsync(IEnumerable<Guid> operationIds, CancellationToken ct = default);

    /// <summary>
    /// Clears all history.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task ClearHistoryAsync(CancellationToken ct = default);

    /// <summary>
    /// Reloads history from disk.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task RefreshAsync(CancellationToken ct = default);

    /// <summary>
    /// Marks an operation as failed.
    /// </summary>
    /// <param name="operationId">The operation ID.</param>
    /// <param name="errorMessage">Error message to record.</param>
    /// <param name="ct">Cancellation token.</param>
    Task MarkAsFailedAsync(Guid operationId, string errorMessage, CancellationToken ct = default);
}
