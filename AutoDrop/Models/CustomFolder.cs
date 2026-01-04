using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace AutoDrop.Models;

/// <summary>
/// Represents a user-defined custom destination folder.
/// Implements INotifyPropertyChanged for UI binding updates.
/// </summary>
public sealed class CustomFolder : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _path = string.Empty;
    private string _icon = "📁";
    private bool _isPinned;

    /// <summary>
    /// Unique identifier for the folder.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name shown in the UI (e.g., "Work Documents").
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>
    /// Full path to the folder.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path
    {
        get => _path;
        set => SetProperty(ref _path, value);
    }

    /// <summary>
    /// Icon identifier for visual representation.
    /// </summary>
    [JsonPropertyName("icon")]
    public string Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    /// <summary>
    /// Whether this folder is pinned to always appear in suggestions.
    /// </summary>
    [JsonPropertyName("pinned")]
    public bool IsPinned
    {
        get => _isPinned;
        set => SetProperty(ref _isPinned, value);
    }

    /// <summary>
    /// Date and time when this folder was added.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the folder name from the path for display purposes.
    /// </summary>
    [JsonIgnore]
    public string DisplayPath => System.IO.Path.GetFileName(Path.TrimEnd(System.IO.Path.DirectorySeparatorChar)) ?? Path;

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;
        
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}
