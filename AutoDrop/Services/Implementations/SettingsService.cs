using AutoDrop.Core.Constants;
using AutoDrop.Models;
using AutoDrop.Services.Interfaces;

namespace AutoDrop.Services.Implementations;

/// <summary>
/// Implementation of settings service for managing application settings.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly IStorageService _storageService;
    private AppSettings? _cachedSettings;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SettingsService(IStorageService storageService)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
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

        await _lock.WaitAsync();
        try
        {
            // Get settings directly without recursively calling GetSettingsAsync (avoids deadlock)
            _cachedSettings ??= await _storageService.ReadJsonAsync<AppSettings>(_storageService.SettingsFilePath)
                                ?? new AppSettings();
            
            // Determine the actual folder path
            // If createSubfolder is true, we create a subfolder with the name inside the given path
            // Otherwise, we use the path directly (for cases where user browses to an existing folder)
            string actualPath;
            if (createSubfolder)
            {
                // Sanitize folder name for file system
                var safeName = SanitizeFolderName(name);
                actualPath = Path.Combine(path, safeName);
            }
            else
            {
                actualPath = path;
            }
            
            // Normalize the path
            actualPath = Path.GetFullPath(actualPath);
            
            // Check for duplicate path
            if (_cachedSettings.CustomFolders.Any(f => 
                string.Equals(f.Path, actualPath, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"A custom folder at '{actualPath}' already exists.");
            }

            // Create the physical folder if it doesn't exist
            if (!Directory.Exists(actualPath))
            {
                Directory.CreateDirectory(actualPath);
            }

            var folder = new CustomFolder
            {
                Name = name,
                Path = actualPath,
                Icon = icon,
                IsPinned = isPinned
            };

            _cachedSettings.CustomFolders.Add(folder);
            await SaveSettingsInternalAsync(_cachedSettings);
            
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

        await _lock.WaitAsync();
        try
        {
            // Get settings directly without recursively calling GetSettingsAsync (avoids deadlock)
            _cachedSettings ??= await _storageService.ReadJsonAsync<AppSettings>(_storageService.SettingsFilePath)
                                ?? new AppSettings();
            
            var existing = _cachedSettings.CustomFolders.FirstOrDefault(f => f.Id == folder.Id);
            
            if (existing == null)
                throw new InvalidOperationException($"Custom folder with ID '{folder.Id}' not found.");

            existing.Name = folder.Name;
            existing.Path = folder.Path;
            existing.Icon = folder.Icon;
            existing.IsPinned = folder.IsPinned;

            await SaveSettingsInternalAsync(_cachedSettings);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveCustomFolderAsync(Guid folderId)
    {
        await _lock.WaitAsync();
        try
        {
            // Get settings directly without recursively calling GetSettingsAsync (avoids deadlock)
            _cachedSettings ??= await _storageService.ReadJsonAsync<AppSettings>(_storageService.SettingsFilePath)
                                ?? new AppSettings();
            
            var folder = _cachedSettings.CustomFolders.FirstOrDefault(f => f.Id == folderId);
            
            if (folder == null)
                return false;

            _cachedSettings.CustomFolders.Remove(folder);
            await SaveSettingsInternalAsync(_cachedSettings);
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
            // Get settings directly without recursively calling GetSettingsAsync (avoids deadlock)
            _cachedSettings ??= await _storageService.ReadJsonAsync<AppSettings>(_storageService.SettingsFilePath)
                                ?? new AppSettings();
            
            var folder = _cachedSettings.CustomFolders.FirstOrDefault(f => f.Id == folderId);
            
            if (folder != null)
            {
                folder.UseCount++;
                await SaveSettingsInternalAsync(_cachedSettings);
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
    
    /// <summary>
    /// Sanitizes a folder name by removing invalid characters.
    /// </summary>
    private static string SanitizeFolderName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.Trim();
    }
}
