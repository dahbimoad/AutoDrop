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
    /// <summary>
    /// Maximum allowed length for folder name.
    /// </summary>
    public const int MaxNameLength = 100;
    
    /// <summary>
    /// Maximum allowed length for folder path.
    /// </summary>
    public const int MaxPathLength = 260;
    
    /// <summary>
    /// Maximum allowed length for icon string.
    /// </summary>
    public const int MaxIconLength = 10;

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
        set
        {
            var validated = ValidateName(value);
            SetProperty(ref _name, validated);
        }
    }

    /// <summary>
    /// Full path to the folder.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path
    {
        get => _path;
        set
        {
            var validated = ValidatePath(value);
            SetProperty(ref _path, validated);
        }
    }

    /// <summary>
    /// Icon identifier for visual representation.
    /// </summary>
    [JsonPropertyName("icon")]
    public string Icon
    {
        get => _icon;
        set
        {
            var validated = ValidateIcon(value);
            SetProperty(ref _icon, validated);
        }
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

    /// <summary>
    /// Validates and sanitizes the folder name.
    /// </summary>
    private static string ValidateName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        
        var trimmed = value.Trim();
        
        // Enforce maximum length
        if (trimmed.Length > MaxNameLength)
            throw new ArgumentException($"Folder name exceeds maximum length of {MaxNameLength} characters.", nameof(value));
        
        // Check for invalid filename characters (folder name used in path construction)
        var invalidChars = System.IO.Path.GetInvalidFileNameChars();
        foreach (var c in trimmed)
        {
            if (invalidChars.Contains(c))
                throw new ArgumentException($"Folder name contains invalid character: '{c}'", nameof(value));
        }
        
        return trimmed;
    }

    /// <summary>
    /// Validates a folder path.
    /// </summary>
    private static string ValidatePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        
        var trimmed = value.Trim();
        
        // Enforce maximum length (Windows MAX_PATH)
        if (trimmed.Length > MaxPathLength)
            throw new ArgumentException($"Folder path exceeds maximum length of {MaxPathLength} characters.", nameof(value));
        
        // Check for invalid path characters
        var invalidChars = System.IO.Path.GetInvalidPathChars();
        foreach (var c in trimmed)
        {
            if (invalidChars.Contains(c))
                throw new ArgumentException($"Folder path contains invalid character: '{c}'", nameof(value));
        }
        
        return trimmed;
    }

    /// <summary>
    /// Validates the icon string.
    /// </summary>
    private static string ValidateIcon(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "📁"; // Default icon
        
        var trimmed = value.Trim();
        
        // Enforce maximum length for icon (emoji + variation selectors)
        if (trimmed.Length > MaxIconLength)
            return trimmed[..MaxIconLength];
        
        return trimmed;
    }

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
