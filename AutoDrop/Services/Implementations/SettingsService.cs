using AutoDrop.Models;
using AutoDrop.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoDrop.Services.Implementations;

/// <summary>
/// Implementation of settings service for managing application settings.
/// </summary>
public sealed class SettingsService : ISettingsService, IDisposable
{
    private readonly IStorageService _storageService;
    private readonly ILogger<SettingsService> _logger;
    private AppSettings? _cachedSettings;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    public SettingsService(IStorageService storageService, ILogger<SettingsService> logger)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _logger = logger;
        _logger.LogDebug("SettingsService initialized");
    }

    /// <inheritdoc />
    public async Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedSettings != null)
            return _cachedSettings;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _cachedSettings ??= await _storageService.ReadJsonAsync<AppSettings>(_storageService.SettingsFilePath, cancellationToken).ConfigureAwait(false)
                                ?? new AppSettings();
            _logger.LogDebug("Settings loaded: {FolderCount} custom folders", _cachedSettings.CustomFolders.Count);
            return _cachedSettings;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _storageService.WriteJsonAsync(_storageService.SettingsFilePath, settings, cancellationToken).ConfigureAwait(false);
            _cachedSettings = settings;
            _logger.LogDebug("Settings saved");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomFolder>> GetCustomFoldersAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        return settings.CustomFolders.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<CustomFolder> AddCustomFolderAsync(string name, string path, string icon = "📁", bool isPinned = false, bool createSubfolder = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Folder name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Folder path cannot be empty.", nameof(path));

        // Determine the actual folder path
        var actualPath = createSubfolder ? Path.Combine(path, name) : path;
        
        _logger.LogInformation("Adding custom folder: {Name} -> {Path} (CreateSubfolder: {CreateSubfolder})", 
            name, actualPath, createSubfolder);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var settings = await GetSettingsAsync(cancellationToken).ConfigureAwait(false);
            
            // Check for duplicate path
            if (settings.CustomFolders.Any(f => 
                string.Equals(f.Path, actualPath, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("Duplicate folder path rejected: {Path}", actualPath);
                throw new InvalidOperationException($"A custom folder with path '{actualPath}' already exists.");
            }

            // Create the folder physically if it doesn't exist
            if (!Directory.Exists(actualPath))
            {
                _logger.LogDebug("Creating physical folder: {Path}", actualPath);
                Directory.CreateDirectory(actualPath);
            }

            var folder = new CustomFolder
            {
                Name = name,
                Path = actualPath,
                Icon = icon,
                IsPinned = isPinned
            };

            settings.CustomFolders.Add(folder);
            await SaveSettingsInternalAsync(settings, cancellationToken).ConfigureAwait(false);
            
            _logger.LogInformation("Custom folder added: {Name} (ID: {Id}) at {Path}", name, folder.Id, actualPath);
            return folder;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task UpdateCustomFolderAsync(CustomFolder folder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folder);
        _logger.LogDebug("Updating custom folder: {Id} - New Path: {Path}", folder.Id, folder.Path);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var settings = await GetSettingsAsync(cancellationToken).ConfigureAwait(false);
            var existing = settings.CustomFolders.FirstOrDefault(f => f.Id == folder.Id);
            
            if (existing == null)
            {
                _logger.LogWarning("Folder not found for update: {Id}", folder.Id);
                throw new InvalidOperationException($"Custom folder with ID '{folder.Id}' not found.");
            }

            // Update all properties
            var oldPath = existing.Path;
            existing.Name = folder.Name;
            existing.Path = folder.Path;
            existing.Icon = folder.Icon;
            existing.IsPinned = folder.IsPinned;

            // Force save to disk
            await SaveSettingsInternalAsync(settings, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Custom folder updated: {Name} - Path changed from '{OldPath}' to '{NewPath}'", 
                folder.Name, oldPath, folder.Path);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveCustomFolderAsync(Guid folderId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Removing custom folder: {Id}", folderId);
        
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var settings = await GetSettingsAsync(cancellationToken).ConfigureAwait(false);
            var folder = settings.CustomFolders.FirstOrDefault(f => f.Id == folderId);
            
            if (folder == null)
            {
                _logger.LogWarning("Folder not found for removal: {Id}", folderId);
                return false;
            }

            settings.CustomFolders.Remove(folder);
            await SaveSettingsInternalAsync(settings, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Custom folder removed: {Name}", folder.Name);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SaveSettingsInternalAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await _storageService.WriteJsonAsync(_storageService.SettingsFilePath, settings, cancellationToken).ConfigureAwait(false);
        _cachedSettings = settings;
    }

    /// <summary>
    /// Disposes resources used by the service.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        
        _lock.Dispose();
        _disposed = true;
        _logger.LogDebug("SettingsService disposed");
    }
}
