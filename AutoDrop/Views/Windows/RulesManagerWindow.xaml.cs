using System.Windows;
using System.Windows.Controls;
using AutoDrop.Models;
using AutoDrop.ViewModels;
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

    public RulesManagerWindow(RulesManagerViewModel viewModel, ISnackbarService snackbarService)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _snackbarService = snackbarService ?? throw new ArgumentNullException(nameof(snackbarService));
        DataContext = _viewModel;
        
        InitializeComponent();
        
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Set up snackbar presenter for this window
        _snackbarService.SetSnackbarPresenter(SnackbarPresenter);
        
        await _viewModel.LoadDataAsync();
    }

    /// <summary>
    /// Handle rule AutoMove checkbox changes - saves to persistence immediately.
    /// </summary>
    private async void OnRuleAutoMoveChanged(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox { DataContext: FileRule rule })
        {
            await _viewModel.SaveRuleAutoMoveAsync(rule);
        }
    }

    /// <summary>
    /// Handle rule Enabled checkbox changes - saves to persistence immediately.
    /// </summary>
    private async void OnRuleEnabledChanged(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox { DataContext: FileRule rule })
        {
            await _viewModel.SaveRuleEnabledAsync(rule);
        }
    }

    /// <summary>
    /// Handle folder Pinned checkbox changes - saves to persistence immediately.
    /// </summary>
    private async void OnFolderPinnedChanged(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox { DataContext: CustomFolder folder })
        {
            await _viewModel.SaveFolderPinnedAsync(folder);
        }
    }
}
