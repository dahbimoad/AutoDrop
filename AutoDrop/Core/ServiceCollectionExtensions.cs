using AutoDrop.Services.Implementations;
using AutoDrop.Services.Interfaces;
using AutoDrop.ViewModels;
using AutoDrop.Views.Windows;
using Microsoft.Extensions.DependencyInjection;
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
        // Core services (Singleton - shared across app)
        services.AddSingleton<IStorageService, StorageService>();
        services.AddSingleton<IRuleService, RuleService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IFileOperationService, FileOperationService>();
        services.AddSingleton<IDestinationSuggestionService, DestinationSuggestionService>();

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
