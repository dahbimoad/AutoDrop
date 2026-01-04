using System.Windows;
using AutoDrop.ViewModels;
using Wpf.Ui.Controls;

namespace AutoDrop.Views.Windows;

/// <summary>
/// Code-behind for the Rules Manager window.
/// </summary>
public partial class RulesManagerWindow : FluentWindow
{
    private readonly RulesManagerViewModel _viewModel;

    public RulesManagerWindow(RulesManagerViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
        
        InitializeComponent();
        
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadDataAsync();
    }
}
