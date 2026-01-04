using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AutoDrop.ViewModels;
using Microsoft.Extensions.Logging;
using Wpf.Ui;

namespace AutoDrop.Views.Windows;

/// <summary>
/// Drop zone window - the main floating window for file dropping.
/// </summary>
public partial class DropZoneWindow : Window
{
    private readonly DropZoneViewModel _viewModel;
    private readonly ISnackbarService _snackbarService;
    private readonly ILogger<DropZoneWindow> _logger;

    public DropZoneWindow(DropZoneViewModel viewModel, ISnackbarService snackbarService, ILogger<DropZoneWindow> logger)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _snackbarService = snackbarService ?? throw new ArgumentNullException(nameof(snackbarService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        InitializeComponent();
        DataContext = _viewModel;

        // Position window in bottom-right corner
        PositionWindow();

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Set up snackbar presenter
        _snackbarService.SetSnackbarPresenter(SnackbarPresenter);
    }

    private void PositionWindow()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 20;
        Top = workArea.Bottom - Height - 20;
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Move;
            _viewModel.HandleDragEnterCommand.Execute(null);
            
            // Visual feedback
            DropZoneBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212));
            DropZoneBorder.BorderThickness = new Thickness(3);
            DropIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.ArrowCircleDown24;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        _viewModel.HandleDragLeaveCommand.Execute(null);
        
        // Reset visual
        DropZoneBorder.BorderBrush = (Brush)FindResource("ControlElevationBorderBrush");
        DropZoneBorder.BorderThickness = new Thickness(2);
        DropIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.ArrowDownload24;
        
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        // Reset visual
        DropZoneBorder.BorderBrush = (Brush)FindResource("ControlElevationBorderBrush");
        DropZoneBorder.BorderThickness = new Thickness(2);
        DropIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.ArrowDownload24;

        try
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    await _viewModel.HandleDropCommand.ExecuteAsync(files);
                    
                    // Show count badge for multiple files
                    if (files.Length > 1)
                    {
                        CountText.Text = $"{files.Length} items";
                        CountBadge.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        CountBadge.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling dropped files");
        }
        
        e.Handled = true;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Allow window dragging
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        // Minimize to system tray
        Hide();
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        // Open Rules Manager window
        var rulesWindow = App.GetService<RulesManagerWindow>();
        rulesWindow.Owner = this;
        rulesWindow.ShowDialog();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    /// <summary>
    /// Shows the window and brings it to foreground.
    /// </summary>
    public void ShowAndActivate()
    {
        Show();
        Activate();
        Topmost = true;
    }
}
