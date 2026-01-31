using System.Text.Json.Serialization;

namespace AutoDrop.Models;

/// <summary>
/// Application settings configuration.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Settings schema version for future migrations.
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>
    /// User-defined custom folders.
    /// </summary>
    [JsonPropertyName("customFolders")]
    public List<CustomFolder> CustomFolders { get; set; } = [];

    /// <summary>
    /// File conflict behavior setting.
    /// </summary>
    [JsonPropertyName("fileConflictBehavior")]
    public FileConflictBehavior FileConflictBehavior { get; set; } = FileConflictBehavior.AutoRename;

    /// <summary>
    /// Whether to compare file contents for duplicate detection.
    /// </summary>
    [JsonPropertyName("compareFileContents")]
    public bool CompareFileContents { get; set; } = true;

    /// <summary>
    /// Auto-rename pattern when file conflicts occur.
    /// </summary>
    [JsonPropertyName("renamePattern")]
    public string RenamePattern { get; set; } = "{name} ({n}){ext}";

    /// <summary>
    /// Whether AI categorization is enabled.
    /// </summary>
    [JsonPropertyName("aiEnabled")]
    [Obsolete("Use AiSettings.Enabled instead")]
    public bool AiEnabled { get; set; }

    /// <summary>
    /// Minimum confidence threshold for AI suggestions.
    /// </summary>
    [JsonPropertyName("aiConfidenceThreshold")]
    [Obsolete("Use AiSettings.ConfidenceThreshold instead")]
    public double AiConfidenceThreshold { get; set; } = 0.7;

    /// <summary>
    /// AI-powered file analysis settings.
    /// </summary>
    [JsonPropertyName("aiSettings")]
    public AiSettings AiSettings { get; set; } = new();

    /// <summary>
    /// Default operation mode (Move or Copy).
    /// </summary>
    [JsonPropertyName("defaultOperationMode")]
    public OperationMode DefaultOperationMode { get; set; } = OperationMode.Move;

    /// <summary>
    /// Whether to minimize to tray on close.
    /// </summary>
    [JsonPropertyName("minimizeToTrayOnClose")]
    public bool MinimizeToTrayOnClose { get; set; } = true;

    /// <summary>
    /// Maximum number of operations to keep in history.
    /// </summary>
    [JsonPropertyName("maxHistoryItems")]
    public int MaxHistoryItems { get; set; } = 20;
}

/// <summary>
/// Defines how to handle file conflicts at destination.
/// </summary>
public enum FileConflictBehavior
{
    /// <summary>
    /// Automatically rename files with numeric suffix.
    /// </summary>
    AutoRename,

    /// <summary>
    /// Prompt user for each conflict.
    /// </summary>
    Ask,

    /// <summary>
    /// Skip files that already exist.
    /// </summary>
    Skip,

    /// <summary>
    /// Replace existing files without prompting.
    /// </summary>
    Replace
}

/// <summary>
/// Defines the default file operation mode.
/// </summary>
public enum OperationMode
{
    /// <summary>
    /// Move files (delete from source).
    /// </summary>
    Move,

    /// <summary>
    /// Copy files (keep source).
    /// </summary>
    Copy
}
