using System.Windows;
using AutoDrop.ViewModels;
using Wpf.Ui.Controls;

namespace AutoDrop.Views.Windows;

/// <summary>
/// AI Settings window for configuring Gemini API integration.
/// </summary>
public partial class AiSettingsWindow : FluentWindow
{
    private readonly AiSettingsViewModel _viewModel;

    public AiSettingsWindow(AiSettingsViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
        
        InitializeComponent();
        
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadSettingsAsync();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
