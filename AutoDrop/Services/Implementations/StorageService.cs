using System.Text.Json;
using AutoDrop.Core.Constants;
using AutoDrop.Services.Interfaces;

namespace AutoDrop.Services.Implementations;

/// <summary>
/// Implementation of storage service for managing application data files.
/// </summary>
public sealed class StorageService : IStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _appDataFolder;

    public StorageService()
    {
        _appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppConstants.AppDataFolderName);
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
        }
    }

    /// <inheritdoc />
    public async Task<T?> ReadJsonAsync<T>(string filePath) where T : class
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
        }
        catch (JsonException)
        {
            // Return null if file is corrupted
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
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, data, JsonOptions);
    }
}
