using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using AutoDrop.Core;
using AutoDrop.Services.Interfaces;
using AutoDrop.Views.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace AutoDrop;

/// <summary>
/// Application entry point and DI container configuration.
/// </summary>
public partial class App : Application
{
    private static readonly ServiceProvider ServiceProvider;

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
        try
        {
            // Ensure app data folder exists
            var storageService = GetService<IStorageService>();
            storageService.EnsureAppDataFolderExists();

            // Show main window
            var dropZoneWindow = GetService<DropZoneWindow>();
            dropZoneWindow.Show();
        }
        catch (Exception ex)
        {
            LogError("Startup Error", ex);
            throw;
        }
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        ServiceProvider.Dispose();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogError("Unhandled Exception", e.Exception);
        
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nSee console for details.",
            "AutoDrop Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    private static void LogError(string context, Exception ex)
    {
        var message = $"""
            
            ========== {context} ==========
            Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
            Message: {ex.Message}
            Type: {ex.GetType().FullName}
            
            Stack Trace:
            {ex.StackTrace}
            
            Inner Exception: {ex.InnerException?.Message ?? "None"}
            ================================
            
            """;
        
        Console.Error.WriteLine(message);
        Debug.WriteLine(message);
        Trace.WriteLine(message);
    }
}