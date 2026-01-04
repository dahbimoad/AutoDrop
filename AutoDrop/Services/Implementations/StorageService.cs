using System.Text.Json;
using AutoDrop.Core.Constants;
using AutoDrop.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoDrop.Services.Implementations;

/// <summary>
/// Implementation of storage service for managing application data files.
/// </summary>
public sealed class StorageService : IStorageService
{
    private readonly ILogger<StorageService> _logger;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _appDataFolder;

    public StorageService(ILogger<StorageService> logger)
    {
        _logger = logger;
        _appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppConstants.AppDataFolderName);
        
        _logger.LogDebug("StorageService initialized. AppDataFolder: {AppDataFolder}", _appDataFolder);
    }

    /// <inheritdoc />
    public string AppDataFolder => _appDataFolder;

    /// <inheritdoc />
    public string RulesFilePath => Path.Combine(_appDataFolder, AppConstants.RulesFileName);

    /// <inheritdoc />
    public string SettingsFilePath => Path.Combine(_appDataFolder, AppConstants.SettingsFileName);

    /// <inheritdoc />
    public void EnsureAppDataFolderExists()
    {
        if (!Directory.Exists(_appDataFolder))
        {
            Directory.CreateDirectory(_appDataFolder);
            _logger.LogInformation("Created app data folder: {Folder}", _appDataFolder);
        }
    }

    /// <inheritdoc />
    public async Task<T?> ReadJsonAsync<T>(string filePath) where T : class
    {
        if (!File.Exists(filePath))
        {
            _logger.LogDebug("File not found, returning null: {FilePath}", filePath);
            return null;
        }

        try
        {
            _logger.LogDebug("Reading JSON file: {FilePath}", filePath);
            await using var stream = File.OpenRead(filePath);
            var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
            _logger.LogDebug("Successfully read {Type} from {FilePath}", typeof(T).Name, filePath);
            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize JSON from {FilePath}. File may be corrupted.", filePath);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task WriteJsonAsync<T>(string filePath, T data) where T : class
    {
        EnsureAppDataFolderExists();

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger.LogDebug("Created directory: {Directory}", directory);
        }

        _logger.LogDebug("Writing {Type} to {FilePath}", typeof(T).Name, filePath);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, data, JsonOptions);
        _logger.LogDebug("Successfully wrote {Type} to {FilePath}", typeof(T).Name, filePath);
    }
}
