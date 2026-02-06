using System.Windows;
using AutoDrop.ViewModels;
using Wpf.Ui.Controls;

namespace AutoDrop.Views.Windows;

/// <summary>
/// Interaction logic for BatchOperationWindow.xaml
/// </summary>
public partial class BatchOperationWindow : FluentWindow
{
    private readonly BatchOperationViewModel _viewModel;

    public BatchOperationWindow(BatchOperationViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        InitializeComponent();

        _viewModel.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
