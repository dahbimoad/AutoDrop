using System.Windows;
using AutoDrop.ViewModels;
using Wpf.Ui.Controls;

namespace AutoDrop.Views.Windows;

/// <summary>
/// Interaction logic for FolderOrganizationWindow.xaml
/// </summary>
public partial class FolderOrganizationWindow : FluentWindow
{
    private readonly FolderOrganizationViewModel _viewModel;

    /// <summary>
    /// The folder path to organize. Set this before showing the dialog.
    /// </summary>
    public string? FolderPath { get; set; }

    public FolderOrganizationWindow(FolderOrganizationViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        InitializeComponent();

        _viewModel.CloseRequested += OnCloseRequested;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(FolderPath))
        {
            await _viewModel.InitializeAsync(FolderPath);
        }
    }

    /// <summary>
    /// Initializes the window with a folder path.
    /// </summary>
    public async Task InitializeAsync(string folderPath)
    {
        await _viewModel.InitializeAsync(folderPath);
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        Loaded -= OnLoaded;
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
