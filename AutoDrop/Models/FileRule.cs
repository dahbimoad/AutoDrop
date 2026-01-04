using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace AutoDrop.Models;

/// <summary>
/// Represents a user-defined rule for automatically moving files with a specific extension.
/// Implements INotifyPropertyChanged for UI binding updates.
/// </summary>
public sealed class FileRule : INotifyPropertyChanged
{
    private string _extension = string.Empty;
    private string _destination = string.Empty;
    private bool _autoMove;
    private bool _isEnabled = true;
    private DateTime? _lastUsedAt;
    private int _useCount;
    private bool _isCustomFolder;

    /// <summary>
    /// The file extension this rule applies to (e.g., ".jpg", ".pdf").
    /// </summary>
    [JsonPropertyName("extension")]
    public required string Extension
    {
        get => _extension;
        set => SetProperty(ref _extension, value);
    }

    /// <summary>
    /// The destination folder path where files should be moved.
    /// </summary>
    [JsonPropertyName("destination")]
    public required string Destination
    {
        get => _destination;
        set => SetProperty(ref _destination, value);
    }

    /// <summary>
    /// Whether to automatically move files without showing the popup.
    /// </summary>
    [JsonPropertyName("autoMove")]
    public bool AutoMove
    {
        get => _autoMove;
        set => SetProperty(ref _autoMove, value);
    }

    /// <summary>
    /// Whether this rule is currently enabled.
    /// </summary>
    [JsonPropertyName("isEnabled")]
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    /// <summary>
    /// Date and time when this rule was created.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date and time when this rule was last used.
    /// </summary>
    [JsonPropertyName("lastUsedAt")]
    public DateTime? LastUsedAt
    {
        get => _lastUsedAt;
        set => SetProperty(ref _lastUsedAt, value);
    }

    /// <summary>
    /// Number of times this rule has been applied.
    /// </summary>
    [JsonPropertyName("useCount")]
    public int UseCount
    {
        get => _useCount;
        set => SetProperty(ref _useCount, value);
    }

    /// <summary>
    /// Gets the destination folder name for display purposes.
    /// </summary>
    [JsonIgnore]
    public string DestinationName => Path.GetFileName(Destination.TrimEnd(Path.DirectorySeparatorChar));

    /// <summary>
    /// Whether this rule points to a custom folder created by the app (vs a system/external folder).
    /// This is set dynamically at runtime, not persisted.
    /// </summary>
    [JsonIgnore]
    public bool IsCustomFolder
    {
        get => _isCustomFolder;
        set => SetProperty(ref _isCustomFolder, value);
    }

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}
