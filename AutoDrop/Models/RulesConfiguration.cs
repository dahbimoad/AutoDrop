using System.Text.Json.Serialization;

namespace AutoDrop.Models;

/// <summary>
/// Root configuration object for storing file rules.
/// </summary>
public sealed class RulesConfiguration
{
    /// <summary>
    /// Collection of user-defined file rules.
    /// </summary>
    [JsonPropertyName("rules")]
    public List<FileRule> Rules { get; set; } = [];

    /// <summary>
    /// Configuration schema version for future migrations.
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;
}
