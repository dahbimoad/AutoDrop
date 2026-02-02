using AutoDrop.ViewModels;
using Wpf.Ui.Controls;

namespace AutoDrop.Views.Windows;

/// <summary>
/// AI Settings window for configuring multi-provider AI integration.
/// </summary>
public partial class AiSettingsWindow : FluentWindow
{
    private readonly AiSettingsViewModel _viewModel;
    
    public AiSettingsWindow(AiSettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        
        // Load settings when window opens
        Loaded += async (_, _) => await _viewModel.LoadSettingsAsync();
    }

    private void OnCancelClick(object sender, System.Windows.RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void OnSaveClick(object sender, System.Windows.RoutedEventArgs e)
    {
        // CRITICAL: Wait for async save to complete before closing
        await _viewModel.SaveSettingsCommand.ExecuteAsync(null);
        DialogResult = true;
        Close();
    }
}
