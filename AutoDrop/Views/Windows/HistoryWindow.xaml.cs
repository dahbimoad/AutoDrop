using System.Windows;
using System.Windows.Controls;
using AutoDrop.ViewModels;
using Microsoft.Extensions.Logging;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace AutoDrop.Views.Windows;

/// <summary>
/// Code-behind for the History window.
/// Displays operation history with full undo capability.
/// </summary>
public partial class HistoryWindow : FluentWindow
{
    private readonly HistoryViewModel _viewModel;
    private readonly ISnackbarService _snackbarService;
    private readonly ILogger<HistoryWindow> _logger;

    public HistoryWindow(
        HistoryViewModel viewModel, 
        ISnackbarService snackbarService, 
        ILogger<HistoryWindow> logger)
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
            
            await _viewModel.LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading history data");
        }
    }

    /// <summary>
    /// Handle DataGrid selection changes to support multi-select.
    /// </summary>
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (sender is System.Windows.Controls.DataGrid dataGrid)
            {
                _viewModel.UpdateSelection(dataGrid.SelectedItems.Cast<object>().ToList());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating selection");
        }
    }
}
