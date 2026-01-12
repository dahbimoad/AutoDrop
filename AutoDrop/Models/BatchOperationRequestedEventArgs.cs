namespace AutoDrop.Models;

/// <summary>
/// Event arguments for when batch operation dialog should be shown.
/// </summary>
public sealed class BatchOperationRequestedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the items that require batch operation processing.
    /// </summary>
    public IReadOnlyList<DroppedItem> Items { get; }

    /// <summary>
    /// Gets or sets whether the batch operation was handled.
    /// If true, the ViewModel should not process these items further.
    /// </summary>
    public bool Handled { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchOperationRequestedEventArgs"/> class.
    /// </summary>
    /// <param name="items">The items requiring batch operation processing.</param>
    public BatchOperationRequestedEventArgs(IReadOnlyList<DroppedItem> items)
    {
        Items = items ?? throw new ArgumentNullException(nameof(items));
    }
}
