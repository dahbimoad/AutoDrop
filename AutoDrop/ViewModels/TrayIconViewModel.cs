using AutoDrop.Services.Interfaces;
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
    private readonly IWindowService _windowService;
    private readonly ILogger<TrayIconViewModel> _logger;

    [ObservableProperty]
    private bool _isDropZoneVisible = true;

    public TrayIconViewModel(
        INavigationService navigationService, 
        IWindowService windowService,
        ILogger<TrayIconViewModel> logger)
    {
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
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
        _windowService.ShowDropZone();
    }

    /// <summary>
    /// Hides the drop zone window to tray.
    /// </summary>
    [RelayCommand]
    private void HideDropZone()
    {
        _logger.LogDebug("HideDropZone requested");
        IsDropZoneVisible = false;
        _windowService.HideDropZone();
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
        _windowService.ShowRulesManager();
    }

    /// <summary>
    /// Opens the AI Settings window.
    /// </summary>
    [RelayCommand]
    private void OpenAiSettings()
    {
        _logger.LogDebug("Opening AI Settings");
        _windowService.ShowAiSettings();
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
}
