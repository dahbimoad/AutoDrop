using System.Windows;
using System.Windows.Threading;
using AutoDrop.Core;
using AutoDrop.Services.Interfaces;
using AutoDrop.Views.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AutoDrop;

/// <summary>
/// Application entry point and DI container configuration.
/// </summary>
public partial class App : Application
{
    private static readonly ServiceProvider ServiceProvider;
    private static ILogger<App>? _logger;

    static App()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices();
        ServiceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// Gets a service from the DI container.
    /// </summary>
    public static T GetService<T>() where T : class
    {
        return ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Gets the service provider instance.
    /// </summary>
    public static IServiceProvider Services => ServiceProvider;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _logger = GetService<ILogger<App>>();
        _logger.LogInformation("========== AutoDrop Starting ==========");
        
        try
        {
            // Ensure app data folder exists
            var storageService = GetService<IStorageService>();
            storageService.EnsureAppDataFolderExists();
            _logger.LogDebug("App data folder verified");

            // Show main window
            var dropZoneWindow = GetService<DropZoneWindow>();
            dropZoneWindow.Show();
            _logger.LogInformation("Application started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error during startup");
            throw;
        }
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _logger?.LogInformation("========== AutoDrop Shutting Down ==========");
        ServiceProvider.Dispose();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unhandled exception caught");
        
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nSee debug output for details.",
            "AutoDrop Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}