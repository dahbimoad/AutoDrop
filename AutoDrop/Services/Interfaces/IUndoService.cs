namespace AutoDrop.Services.Interfaces;

/// <summary>
/// Service responsible for managing undo operations.
/// Follows Single Responsibility Principle - only handles undo state.
/// </summary>
public interface IUndoService
{
    /// <summary>
    /// Event raised when an undo operation becomes available.
    /// </summary>
    event EventHandler<UndoAvailableEventArgs>? UndoAvailable;

    /// <summary>
    /// Event raised when an undo operation is executed.
    /// </summary>
    event EventHandler<UndoExecutedEventArgs>? UndoExecuted;

    /// <summary>
    /// Indicates whether an undo operation is currently available.
    /// </summary>
    bool CanUndo { get; }

    /// <summary>
    /// Gets the description of the current undoable operation.
    /// </summary>
    string? CurrentOperationDescription { get; }

    /// <summary>
    /// Gets the count of pending undo operations.
    /// </summary>
    int PendingOperationsCount { get; }

    /// <summary>
    /// Registers a new undoable operation (adds to batch).
    /// </summary>
    /// <param name="description">Human-readable description of the operation.</param>
    /// <param name="undoAction">The action to execute when undoing.</param>
    /// <param name="expirationSeconds">Seconds until undo expires. Default is 10 seconds.</param>
    void RegisterOperation(string description, Func<Task<bool>> undoAction, int expirationSeconds = 10);

    /// <summary>
    /// Executes ALL pending undo operations.
    /// </summary>
    /// <returns>True if all undos were successful, false if any failed.</returns>
    Task<bool> ExecuteUndoAsync();

    /// <summary>
    /// Clears all pending undo operations.
    /// </summary>
    void ClearUndo();
}

/// <summary>
/// Event args for when an undo operation becomes available.
/// </summary>
public sealed class UndoAvailableEventArgs : EventArgs
{
    public required string Description { get; init; }
    public required int ExpirationSeconds { get; init; }
    public required int TotalCount { get; init; }
}

/// <summary>
/// Event args for when an undo operation is executed.
/// </summary>
public sealed class UndoExecutedEventArgs : EventArgs
{
    public required bool Success { get; init; }
    public required string Description { get; init; }
    public required int UndoneCount { get; init; }
    public required int FailedCount { get; init; }
    public string? ErrorMessage { get; init; }
}
