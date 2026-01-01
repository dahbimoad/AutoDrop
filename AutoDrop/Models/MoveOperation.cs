namespace AutoDrop.Models;

/// <summary>
/// Represents a completed file move operation for undo support.
/// </summary>
public sealed class MoveOperation
{
    /// <summary>
    /// Unique identifier for this operation.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Original source path before the move.
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// Destination path after the move.
    /// </summary>
    public required string DestinationPath { get; init; }

    /// <summary>
    /// File or folder name.
    /// </summary>
    public required string ItemName { get; init; }

    /// <summary>
    /// Whether the moved item was a directory.
    /// </summary>
    public bool IsDirectory { get; init; }

    /// <summary>
    /// Timestamp when the operation was performed.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this operation can still be undone.
    /// </summary>
    public bool CanUndo { get; set; } = true;
}
