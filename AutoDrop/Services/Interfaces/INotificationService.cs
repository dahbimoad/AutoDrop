using AutoDrop.Models;

namespace AutoDrop.Services.Interfaces;

/// <summary>
/// Service responsible for displaying notifications to the user.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Shows a success notification for a completed move operation.
    /// </summary>
    /// <param name="operation">The completed operation.</param>
    void ShowMoveSuccess(MoveOperation operation);

    /// <summary>
    /// Shows a success notification for an auto-move operation.
    /// </summary>
    /// <param name="itemName">Name of the moved item.</param>
    /// <param name="destinationName">Name of the destination folder.</param>
    void ShowAutoMoveSuccess(string itemName, string destinationName);

    /// <summary>
    /// Shows an error notification.
    /// </summary>
    /// <param name="title">Error title.</param>
    /// <param name="message">Error message.</param>
    void ShowError(string title, string message);

    /// <summary>
    /// Shows a success notification.
    /// </summary>
    /// <param name="title">Notification title.</param>
    /// <param name="message">Notification message.</param>
    void ShowSuccess(string title, string message);

    /// <summary>
    /// Shows an informational notification.
    /// </summary>
    /// <param name="title">Notification title.</param>
    /// <param name="message">Notification message.</param>
    void ShowInfo(string title, string message);

    /// <summary>
    /// Shows a notification that undo was successful.
    /// </summary>
    /// <param name="itemName">Name of the item that was restored.</param>
    void ShowUndoSuccess(string itemName);
}

