namespace AutoDrop.Services.Interfaces;

/// <summary>
/// Service responsible for managing application storage paths.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Gets the application data folder path (%AppData%/AutoDrop).
    /// </summary>
    string AppDataFolder { get; }

    /// <summary>
    /// Gets the full path to the rules configuration file.
    /// </summary>
    string RulesFilePath { get; }

    /// <summary>
    /// Gets the full path to the settings file.
    /// </summary>
    string SettingsFilePath { get; }

    /// <summary>
    /// Gets the full path to the log files folder.
    /// </summary>
    string LogsFolder { get; }

    /// <summary>
    /// Ensures the application data folder exists.
    /// </summary>
    void EnsureAppDataFolderExists();

    /// <summary>
    /// Reads JSON content from a file.
    /// </summary>
    /// <typeparam name="T">Type to deserialize to.</typeparam>
    /// <param name="filePath">Path to the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deserialized object or default value.</returns>
    Task<T?> ReadJsonAsync<T>(string filePath, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Writes an object as JSON to a file.
    /// </summary>
    /// <typeparam name="T">Type of object.</typeparam>
    /// <param name="filePath">Path to the file.</param>
    /// <param name="data">Object to serialize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WriteJsonAsync<T>(string filePath, T data, CancellationToken cancellationToken = default) where T : class;
}
