using AutoDrop.Views.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Wpf.Ui;

namespace AutoDrop.ViewModels;

/// <summary>
/// ViewModel for system tray context menu and tray icon interactions.
/// </summary>
public partial class TrayIconViewModel : Base.ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly ILogger<TrayIconViewModel> _logger;

    [ObservableProperty]
    private bool _isDropZoneVisible = true;

    public TrayIconViewModel(INavigationService navigationService, ILogger<TrayIconViewModel> logger)
    {
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _logger = logger;
        _logger.LogDebug("TrayIconViewModel initialized");
    }

    /// <summary>
    /// Shows or restores the drop zone window.
    /// </summary>
    [RelayCommand]
    private void ShowDropZone()
    {
        _logger.LogDebug("ShowDropZone requested");
        IsDropZoneVisible = true;
        OnShowDropZoneRequested?.Invoke();
    }

    /// <summary>
    /// Hides the drop zone window to tray.
    /// </summary>
    [RelayCommand]
    private void HideDropZone()
    {
        _logger.LogDebug("HideDropZone requested");
        IsDropZoneVisible = false;
        OnHideDropZoneRequested?.Invoke();
    }

    /// <summary>
    /// Toggles drop zone visibility.
    /// </summary>
    [RelayCommand]
    private void ToggleDropZone()
    {
        if (IsDropZoneVisible)
        {
            HideDropZone();
        }
        else
        {
            ShowDropZone();
        }
    }

    /// <summary>
    /// Opens the Rules Manager window.
    /// </summary>
    [RelayCommand]
    private void OpenRulesManager()
    {
        _logger.LogDebug("Opening Rules Manager");
        var rulesWindow = App.GetService<RulesManagerWindow>();
        rulesWindow.ShowDialog();
    }

    /// <summary>
    /// Exits the application.
    /// </summary>
    [RelayCommand]
    private void Exit()
    {
        _logger.LogInformation("Application exit requested by user");
        System.Windows.Application.Current.Shutdown();
    }

    /// <summary>
    /// Event raised when show drop zone is requested.
    /// </summary>
    public event Action? OnShowDropZoneRequested;

    /// <summary>
    /// Event raised when hide drop zone is requested.
    /// </summary>
    public event Action? OnHideDropZoneRequested;
}
