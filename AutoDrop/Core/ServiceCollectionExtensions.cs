using AutoDrop.Core.Constants;
using AutoDrop.Services.AI;
using AutoDrop.Services.AI.Providers;
using AutoDrop.Services.Implementations;
using AutoDrop.Services.Interfaces;
using AutoDrop.ViewModels;
using AutoDrop.Views.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
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
        // Configure file logging path
        var logsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppConstants.AppDataFolderName,
            "Logs");
        
        // Ensure logs folder exists
        Directory.CreateDirectory(logsFolder);
        
        var logFilePath = Path.Combine(logsFolder, "autodrop-.log");

        // Configure Serilog for file logging
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            #if DEBUG
            .MinimumLevel.Debug()
            #else
            .MinimumLevel.Information()
            #endif
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7, // Keep 7 days of logs
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                shared: true)
            .CreateLogger();

        // Configure logging with Serilog + Debug output
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, dispose: true);
            
            #if DEBUG
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Debug);
            #else
            builder.SetMinimumLevel(LogLevel.Information);
            #endif
        });

        // Core services (Singleton - shared across app)
        services.AddSingleton<IStorageService, StorageService>();
        services.AddSingleton<ICredentialService, CredentialService>();
        services.AddSingleton<IRuleService, RuleService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IFileOperationService, FileOperationService>();
        services.AddSingleton<IDestinationSuggestionService, DestinationSuggestionService>();
        services.AddSingleton<IUndoService, UndoService>();
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<IDuplicateDetectionService, DuplicateDetectionService>();
        services.AddSingleton<IBatchOperationService, BatchOperationService>();

        // AI Services (Multi-Provider)
        // Providers are registered as singletons and injected into AiService via IEnumerable<IAiProvider>
        services.AddSingleton<IAiProvider, OpenAiProvider>();
        services.AddSingleton<IAiProvider, ClaudeProvider>();
        services.AddSingleton<IAiProvider, GeminiProvider>();
        services.AddSingleton<IAiProvider, GroqProvider>();
        services.AddSingleton<IAiProvider, OllamaProvider>();
        services.AddSingleton<IAiService, AiService>();

        // WPF UI Services
        services.AddSingleton<ISnackbarService, SnackbarService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<INotificationService, NotificationService>();

        // ViewModels (Transient - new instance per request)
        services.AddTransient<DropZoneViewModel>();
        services.AddTransient<TrayIconViewModel>();
        services.AddTransient<RulesManagerViewModel>();
        services.AddTransient<BatchOperationViewModel>();
        services.AddTransient<AiSettingsViewModel>();

        // Windows
        services.AddSingleton<DropZoneWindow>();
        services.AddTransient<RulesManagerWindow>();
        services.AddTransient<BatchOperationWindow>();
        services.AddTransient<AiSettingsWindow>();

        return services;
    }
}
