using System.Windows;
using System.Windows.Controls;
using AutoDrop.Models;
using AutoDrop.ViewModels;
using Microsoft.Extensions.Logging;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace AutoDrop.Views.Windows;

/// <summary>
/// Code-behind for the Rules Manager window.
/// </summary>
public partial class RulesManagerWindow : FluentWindow
{
    private readonly RulesManagerViewModel _viewModel;
    private readonly ISnackbarService _snackbarService;
    private readonly ILogger<RulesManagerWindow> _logger;

    public RulesManagerWindow(RulesManagerViewModel viewModel, ISnackbarService snackbarService, ILogger<RulesManagerWindow> logger)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _snackbarService = snackbarService ?? throw new ArgumentNullException(nameof(snackbarService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        DataContext = _viewModel;
        
        InitializeComponent();
        
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Set up snackbar presenter for this window
            _snackbarService.SetSnackbarPresenter(SnackbarPresenter);
            
            await _viewModel.LoadDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading rules manager data");
        }
    }

    /// <summary>
    /// Handle rule AutoMove checkbox changes - saves to persistence immediately.
    /// </summary>
    private async void OnRuleAutoMoveChanged(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is System.Windows.Controls.CheckBox { DataContext: FileRule rule })
            {
                await _viewModel.SaveRuleAutoMoveAsync(rule);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving rule AutoMove setting");
        }
    }

    /// <summary>
    /// Handle rule Enabled checkbox changes - saves to persistence immediately.
    /// </summary>
    private async void OnRuleEnabledChanged(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is System.Windows.Controls.CheckBox { DataContext: FileRule rule })
            {
                await _viewModel.SaveRuleEnabledAsync(rule);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving rule Enabled setting");
        }
    }

    /// <summary>
    /// Handle folder Pinned checkbox changes - saves to persistence immediately.
    /// </summary>
    private async void OnFolderPinnedChanged(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is System.Windows.Controls.CheckBox { DataContext: CustomFolder folder })
            {
                await _viewModel.SaveFolderPinnedAsync(folder);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving folder Pinned setting");
        }
    }
}
