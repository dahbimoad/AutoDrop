using AutoDrop.Services.Implementations;
using AutoDrop.Services.Interfaces;
using AutoDrop.ViewModels;
using AutoDrop.Views.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wpf.Ui;

namespace AutoDrop.Core;

/// <summary>
/// Configures dependency injection for the application.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all application services with the DI container.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Configure logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            
            // Debug output (Visual Studio Output window)
            builder.AddDebug();
            
            // Console output (for development)
            #if DEBUG
            builder.AddConsole();
            builder.AddFilter("AutoDrop", LogLevel.Debug);
            #else
            builder.AddFilter("AutoDrop", LogLevel.Warning);
            #endif
        });

        // Core services (Singleton - shared across app)
        services.AddSingleton<IStorageService, StorageService>();
        services.AddSingleton<IRuleService, RuleService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IFileOperationService, FileOperationService>();
        services.AddSingleton<IDestinationSuggestionService, DestinationSuggestionService>();
        services.AddSingleton<IUndoService, UndoService>();

        // WPF UI Services
        services.AddSingleton<ISnackbarService, SnackbarService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<INotificationService, NotificationService>();

        // ViewModels (Transient - new instance per request)
        services.AddTransient<DropZoneViewModel>();
        services.AddTransient<TrayIconViewModel>();
        services.AddTransient<RulesManagerViewModel>();

        // Windows
        services.AddSingleton<DropZoneWindow>();
        services.AddTransient<RulesManagerWindow>();

        return services;
    }
}
