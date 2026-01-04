using System.Text.Json.Serialization;

namespace AutoDrop.Models;

/// <summary>
/// Represents a user-defined custom destination folder.
/// </summary>
public sealed class CustomFolder
{
    /// <summary>
    /// Unique identifier for the folder.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name shown in the UI (e.g., "Work Documents").
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Full path to the folder.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; set; }

    /// <summary>
    /// Icon identifier for visual representation.
    /// </summary>
    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "📁";

    /// <summary>
    /// Whether this folder is pinned to always appear in suggestions.
    /// </summary>
    [JsonPropertyName("pinned")]
    public bool IsPinned { get; set; }

    /// <summary>
    /// Date and time when this folder was added.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of times files were moved to this folder.
    /// </summary>
    [JsonPropertyName("useCount")]
    public int UseCount { get; set; }
}
