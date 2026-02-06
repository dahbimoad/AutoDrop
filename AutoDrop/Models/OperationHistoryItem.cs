using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace AutoDrop.Models;

/// <summary>
/// Status of an operation in the history.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OperationStatus
{
    /// <summary>Operation completed successfully.</summary>
    Success,
    
    /// <summary>Operation failed.</summary>
    Failed,
    
    /// <summary>Operation was undone.</summary>
    Undone,
    
    /// <summary>Operation is pending.</summary>
    Pending
}

/// <summary>
/// Type of file operation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OperationType
{
    /// <summary>File was moved.</summary>
    Move,
    
    /// <summary>File was copied.</summary>
    Copy,
    
    /// <summary>File was renamed.</summary>
    Rename
}

/// <summary>
/// Represents a recorded file operation for history tracking and undo support.
/// Every move is reversible - zero risk.
/// Implements INotifyPropertyChanged so UI updates when status changes.
/// </summary>
public sealed class OperationHistoryItem : INotifyPropertyChanged
{
    private OperationStatus _status = OperationStatus.Success;
    private DateTime? _undoneAt;
    private string? _errorMessage;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Unique identifier for this operation.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Original source path before the operation.
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// Destination path after the operation.
    /// </summary>
    public required string DestinationPath { get; init; }

    /// <summary>
    /// File or folder name.
    /// </summary>
    public required string ItemName { get; init; }

    /// <summary>
    /// Whether the item is a directory.
    /// </summary>
    public bool IsDirectory { get; init; }

    /// <summary>
    /// File size in bytes (0 for directories).
    /// </summary>
    public long SizeBytes { get; init; }

    /// <summary>
    /// Type of operation performed.
    /// </summary>
    public OperationType OperationType { get; init; } = OperationType.Move;

    /// <summary>
    /// Current status of the operation.
    /// </summary>
    public OperationStatus Status 
    { 
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(StatusIcon));
            }
        }
    }

    /// <summary>
    /// Timestamp when the operation was performed (UTC).
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the operation was undone (UTC), if applicable.
    /// </summary>
    public DateTime? UndoneAt 
    { 
        get => _undoneAt;
        set
        {
            if (_undoneAt != value)
            {
                _undoneAt = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// AI confidence score when the operation was suggested (0.0 - 1.0).
    /// Null if operation was manual or rule-based.
    /// </summary>
    public double? AiConfidence { get; init; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage 
    { 
        get => _errorMessage;
        set
        {
            if (_errorMessage != value)
            {
                _errorMessage = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Whether the item at destination still exists and can be undone.
    /// </summary>
    [JsonIgnore]
    public bool CanUndo => Status == OperationStatus.Success && 
                           (IsDirectory ? Directory.Exists(DestinationPath) : File.Exists(DestinationPath));

    /// <summary>
    /// Gets a human-readable display of the destination folder.
    /// </summary>
    [JsonIgnore]
    public string DestinationDisplay => Path.GetDirectoryName(DestinationPath) ?? DestinationPath;

    /// <summary>
    /// Gets a human-readable time since the operation.
    /// </summary>
    [JsonIgnore]
    public string TimeAgo
    {
        get
        {
            var elapsed = DateTime.UtcNow - Timestamp;
            return elapsed.TotalMinutes < 1 ? "Just now" :
                   elapsed.TotalMinutes < 60 ? $"{(int)elapsed.TotalMinutes}m ago" :
                   elapsed.TotalHours < 24 ? $"{(int)elapsed.TotalHours}h ago" :
                   elapsed.TotalDays < 7 ? $"{(int)elapsed.TotalDays}d ago" :
                   Timestamp.ToLocalTime().ToString("MMM d");
        }
    }

    /// <summary>
    /// Status icon for display.
    /// </summary>
    [JsonIgnore]
    public string StatusIcon => Status switch
    {
        OperationStatus.Success => "✓",
        OperationStatus.Failed => "✗",
        OperationStatus.Undone => "↩",
        OperationStatus.Pending => "⏳",
        _ => "?"
    };

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Container for operation history with metadata.
/// </summary>
public sealed class OperationHistoryData
{
    /// <summary>
    /// Schema version for future migrations.
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// Maximum number of items to retain.
    /// </summary>
    public int MaxItems { get; init; } = 100;

    /// <summary>
    /// The history items.
    /// </summary>
    public List<OperationHistoryItem> Items { get; init; } = [];
}
