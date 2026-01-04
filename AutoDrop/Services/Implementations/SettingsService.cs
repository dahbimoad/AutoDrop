using AutoDrop.Models;
using AutoDrop.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoDrop.Services.Implementations;

/// <summary>
/// Implementation of settings service for managing application settings.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly IStorageService _storageService;
    private readonly ILogger<SettingsService> _logger;
    private AppSettings? _cachedSettings;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SettingsService(IStorageService storageService, ILogger<SettingsService> logger)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _logger = logger;
        _logger.LogDebug("SettingsService initialized");
    }

    /// <inheritdoc />
    public async Task<AppSettings> GetSettingsAsync()
    {
        if (_cachedSettings != null)
            return _cachedSettings;

        await _lock.WaitAsync();
        try
        {
            _cachedSettings ??= await _storageService.ReadJsonAsync<AppSettings>(_storageService.SettingsFilePath)
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
    public async Task SaveSettingsAsync(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _lock.WaitAsync();
        try
        {
            await _storageService.WriteJsonAsync(_storageService.SettingsFilePath, settings);
            _cachedSettings = settings;
            _logger.LogDebug("Settings saved");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomFolder>> GetCustomFoldersAsync()
    {
        var settings = await GetSettingsAsync();
        return settings.CustomFolders.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<CustomFolder> AddCustomFolderAsync(string name, string path, string icon = "📁", bool isPinned = false, bool createSubfolder = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Folder name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Folder path cannot be empty.", nameof(path));

        // Determine the actual folder path
        var actualPath = createSubfolder ? Path.Combine(path, name) : path;
        
        _logger.LogInformation("Adding custom folder: {Name} -> {Path} (CreateSubfolder: {CreateSubfolder})", 
            name, actualPath, createSubfolder);

        await _lock.WaitAsync();
        try
        {
            var settings = await GetSettingsAsync();
            
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
            await SaveSettingsInternalAsync(settings);
            
            _logger.LogInformation("Custom folder added: {Name} (ID: {Id}) at {Path}", name, folder.Id, actualPath);
            return folder;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task UpdateCustomFolderAsync(CustomFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        _logger.LogDebug("Updating custom folder: {Id}", folder.Id);

        await _lock.WaitAsync();
        try
        {
            var settings = await GetSettingsAsync();
            var existing = settings.CustomFolders.FirstOrDefault(f => f.Id == folder.Id);
            
            if (existing == null)
            {
                _logger.LogWarning("Folder not found for update: {Id}", folder.Id);
                throw new InvalidOperationException($"Custom folder with ID '{folder.Id}' not found.");
            }

            existing.Name = folder.Name;
            existing.Path = folder.Path;
            existing.Icon = folder.Icon;
            existing.IsPinned = folder.IsPinned;

            await SaveSettingsInternalAsync(settings);
            _logger.LogDebug("Custom folder updated: {Name}", folder.Name);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveCustomFolderAsync(Guid folderId)
    {
        _logger.LogInformation("Removing custom folder: {Id}", folderId);
        
        await _lock.WaitAsync();
        try
        {
            var settings = await GetSettingsAsync();
            var folder = settings.CustomFolders.FirstOrDefault(f => f.Id == folderId);
            
            if (folder == null)
            {
                _logger.LogWarning("Folder not found for removal: {Id}", folderId);
                return false;
            }

            settings.CustomFolders.Remove(folder);
            await SaveSettingsInternalAsync(settings);
            _logger.LogInformation("Custom folder removed: {Name}", folder.Name);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task IncrementFolderUsageAsync(Guid folderId)
    {
        await _lock.WaitAsync();
        try
        {
            var settings = await GetSettingsAsync();
            var folder = settings.CustomFolders.FirstOrDefault(f => f.Id == folderId);
            
            if (folder != null)
            {
                folder.UseCount++;
                await SaveSettingsInternalAsync(settings);
                _logger.LogDebug("Folder usage incremented: {Name} -> {Count}", folder.Name, folder.UseCount);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SaveSettingsInternalAsync(AppSettings settings)
    {
        await _storageService.WriteJsonAsync(_storageService.SettingsFilePath, settings);
        _cachedSettings = settings;
    }
}
