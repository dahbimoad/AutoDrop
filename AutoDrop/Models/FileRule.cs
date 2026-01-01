using System.Text.Json.Serialization;

namespace AutoDrop.Models;

/// <summary>
/// Represents a user-defined rule for automatically moving files with a specific extension.
/// </summary>
public sealed class FileRule
{
    /// <summary>
    /// The file extension this rule applies to (e.g., ".jpg", ".pdf").
    /// </summary>
    [JsonPropertyName("extension")]
    public required string Extension { get; set; }

    /// <summary>
    /// The destination folder path where files should be moved.
    /// </summary>
    [JsonPropertyName("destination")]
    public required string Destination { get; set; }

    /// <summary>
    /// Date and time when this rule was created.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date and time when this rule was last used.
    /// </summary>
    [JsonPropertyName("lastUsedAt")]
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Number of times this rule has been applied.
    /// </summary>
    [JsonPropertyName("useCount")]
    public int UseCount { get; set; }
}
