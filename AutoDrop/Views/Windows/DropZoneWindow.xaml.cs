using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AutoDrop.ViewModels;
using Wpf.Ui;

namespace AutoDrop.Views.Windows;

/// <summary>
/// Drop zone window - the main floating window for file dropping.
/// </summary>
public partial class DropZoneWindow : Window
{
    private readonly DropZoneViewModel _viewModel;
    private readonly ISnackbarService _snackbarService;

    public DropZoneWindow(DropZoneViewModel viewModel, ISnackbarService snackbarService)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _snackbarService = snackbarService ?? throw new ArgumentNullException(nameof(snackbarService));

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
