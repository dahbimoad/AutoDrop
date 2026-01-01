using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wpf.Ui;

namespace AutoDrop.ViewModels;

/// <summary>
/// ViewModel for system tray context menu and tray icon interactions.
/// </summary>
public partial class TrayIconViewModel : Base.ViewModelBase
{
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private bool _isDropZoneVisible = true;

    public TrayIconViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
    }

    /// <summary>
    /// Shows or restores the drop zone window.
    /// </summary>
    [RelayCommand]
    private void ShowDropZone()
    {
        IsDropZoneVisible = true;
        OnShowDropZoneRequested?.Invoke();
    }

    /// <summary>
    /// Hides the drop zone window to tray.
    /// </summary>
    [RelayCommand]
    private void HideDropZone()
    {
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
    /// Exits the application.
    /// </summary>
    [RelayCommand]
    private static void Exit()
    {
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
