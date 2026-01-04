using AutoDrop.Core.Constants;
using AutoDrop.Models;
using AutoDrop.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace AutoDrop.Services.Implementations;

/// <summary>
/// Implementation of notification service using WPF UI Snackbar.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly ISnackbarService _snackbarService;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ISnackbarService snackbarService, ILogger<NotificationService> logger)
    {
        _snackbarService = snackbarService ?? throw new ArgumentNullException(nameof(snackbarService));
        _logger = logger;
        _logger.LogDebug("NotificationService initialized");
    }

    /// <inheritdoc />
    public void ShowMoveSuccess(MoveOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var destinationName = Path.GetFileName(Path.GetDirectoryName(operation.DestinationPath) ?? operation.DestinationPath);
        var message = $"Moved {operation.ItemName} → {destinationName}";

        _logger.LogDebug("Showing move success: {Message}", message);

        _snackbarService.Show(
            "Success",
            message,
            ControlAppearance.Success,
            new SymbolIcon(SymbolRegular.Checkmark24),
            TimeSpan.FromMilliseconds(AppConstants.ToastDurationMs));
    }

    /// <inheritdoc />
    public void ShowAutoMoveSuccess(string itemName, string destinationName)
    {
        var message = $"Auto-moved {itemName} → {destinationName}";

        _logger.LogDebug("Showing auto-move success: {Message}", message);

        _snackbarService.Show(
            "Auto-Move",
            message,
            ControlAppearance.Success,
            new SymbolIcon(SymbolRegular.ArrowForward24),
            TimeSpan.FromMilliseconds(AppConstants.ToastDurationMs));
    }

    /// <inheritdoc />
    public void ShowError(string title, string message)
    {
        _logger.LogDebug("Showing error notification: {Title} - {Message}", title, message);
        
        _snackbarService.Show(
            title,
            message,
            ControlAppearance.Danger,
            new SymbolIcon(SymbolRegular.ErrorCircle24),
            TimeSpan.FromMilliseconds(AppConstants.ToastDurationMs));
    }

    /// <inheritdoc />
    public void ShowSuccess(string title, string message)
    {
        _logger.LogDebug("Showing success notification: {Title}", title);
        
        _snackbarService.Show(
            title,
            message,
            ControlAppearance.Success,
            new SymbolIcon(SymbolRegular.Checkmark24),
            TimeSpan.FromMilliseconds(AppConstants.ToastDurationMs));
    }

    /// <inheritdoc />
    public void ShowInfo(string title, string message)
    {
        _logger.LogDebug("Showing info notification: {Title}", title);
        
        _snackbarService.Show(
            title,
            message,
            ControlAppearance.Info,
            new SymbolIcon(SymbolRegular.Info24),
            TimeSpan.FromMilliseconds(AppConstants.ToastDurationMs));
    }

    /// <inheritdoc />
    public void ShowUndoSuccess(string itemName)
    {
        _logger.LogDebug("Showing undo success for: {ItemName}", itemName);
        
        _snackbarService.Show(
            "Restored",
            $"{itemName} has been moved back to its original location.",
            ControlAppearance.Caution,
            new SymbolIcon(SymbolRegular.ArrowUndo24),
            TimeSpan.FromMilliseconds(AppConstants.ToastDurationMs));
    }
}
