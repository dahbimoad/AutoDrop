using AutoDrop.Services.Interfaces;
using AutoDrop.Views.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AutoDrop.Services.Implementations;

/// <summary>
/// Implementation of window service for managing application windows.
/// </summary>
public sealed class WindowService : IWindowService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WindowService> _logger;

    public WindowService(IServiceProvider serviceProvider, ILogger<WindowService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogDebug("WindowService initialized");
    }

    /// <inheritdoc />
    public void ShowRulesManager()
    {
        _logger.LogDebug("Opening Rules Manager window");
        try
        {
            var rulesWindow = _serviceProvider.GetRequiredService<RulesManagerWindow>();
            rulesWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Rules Manager window");
            throw;
        }
    }

    /// <inheritdoc />
    public void ShowDropZone()
    {
        _logger.LogDebug("Showing Drop Zone window");
        try
        {
            var dropZoneWindow = _serviceProvider.GetRequiredService<DropZoneWindow>();
            dropZoneWindow.Show();
            dropZoneWindow.Activate();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show Drop Zone window");
            throw;
        }
    }

    /// <inheritdoc />
    public void HideDropZone()
    {
        _logger.LogDebug("Hiding Drop Zone window");
        try
        {
            var dropZoneWindow = _serviceProvider.GetRequiredService<DropZoneWindow>();
            dropZoneWindow.Hide();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hide Drop Zone window");
            throw;
        }
    }

    /// <inheritdoc />
    public string? ShowFolderBrowserDialog(string? title = null, string? initialDirectory = null)
    {
        _logger.LogDebug("Opening folder browser dialog");
        
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = title ?? "Select Folder"
        };

        if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    /// <inheritdoc />
    public string? ShowSaveFileDialog(string? title = null, string? filter = null, string? defaultFileName = null)
    {
        _logger.LogDebug("Opening save file dialog");
        
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = title ?? "Save File",
            Filter = filter ?? "All Files (*.*)|*.*"
        };

        if (!string.IsNullOrEmpty(defaultFileName))
        {
            dialog.FileName = defaultFileName;
        }

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <inheritdoc />
    public string? ShowOpenFileDialog(string? title = null, string? filter = null)
    {
        _logger.LogDebug("Opening open file dialog");
        
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = title ?? "Open File",
            Filter = filter ?? "All Files (*.*)|*.*"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <inheritdoc />
    public void ShowAiSettings()
    {
        _logger.LogDebug("Opening AI Settings window");
        try
        {
            var aiSettingsWindow = _serviceProvider.GetRequiredService<AiSettingsWindow>();
            aiSettingsWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open AI Settings window");
            throw;
        }
    }

    /// <inheritdoc />
    public void ShowFolderOrganization(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        
        _logger.LogDebug("Opening Folder Organization window for {Folder}", folderPath);
        try
        {
            var folderOrgWindow = _serviceProvider.GetRequiredService<FolderOrganizationWindow>();
            folderOrgWindow.FolderPath = folderPath;
            folderOrgWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Folder Organization window");
            throw;
        }
    }

    /// <inheritdoc />
    public void ShowHistory()
    {
        _logger.LogDebug("Opening History window");
        try
        {
            var historyWindow = _serviceProvider.GetRequiredService<HistoryWindow>();
            historyWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open History window");
            throw;
        }
    }
}
