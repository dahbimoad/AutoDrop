using AutoDrop.Services.Interfaces;

namespace AutoDrop.Models;

/// <summary>
/// Event arguments for duplicate handling requests.
/// </summary>
public sealed class DuplicateHandlingRequestedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the duplicate check result with file comparison info.
    /// </summary>
    public DuplicateCheckResult DuplicateResult { get; }

    /// <summary>
    /// Gets whether there are more duplicates to process after this one.
    /// </summary>
    public bool HasMoreDuplicates { get; }

    /// <summary>
    /// Gets or sets the user's selected handling choice.
    /// </summary>
    public DuplicateHandling SelectedHandling { get; set; } = DuplicateHandling.Ask;

    /// <summary>
    /// Gets or sets whether the user wants to apply this choice to all remaining duplicates.
    /// </summary>
    public bool ApplyToAll { get; set; }

    /// <summary>
    /// Gets or sets whether the operation was cancelled by the user.
    /// </summary>
    public bool WasCancelled { get; set; }

    /// <summary>
    /// Gets or sets whether this event has been handled by a subscriber.
    /// </summary>
    public bool Handled { get; set; }

    public DuplicateHandlingRequestedEventArgs(DuplicateCheckResult duplicateResult, bool hasMoreDuplicates = false)
    {
        DuplicateResult = duplicateResult ?? throw new ArgumentNullException(nameof(duplicateResult));
        HasMoreDuplicates = hasMoreDuplicates;
    }
}
