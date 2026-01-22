namespace AutoDrop.Models;

/// <summary>
/// Represents a suggested destination folder for a dropped file.
/// </summary>
public sealed class DestinationSuggestion
{
    /// <summary>
    /// Display name for the destination (e.g., "Pictures", "Documents").
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Full path to the destination folder.
    /// </summary>
    public required string FullPath { get; set; }

    /// <summary>
    /// Icon identifier for the destination.
    /// </summary>
    public string? IconGlyph { get; set; }

    /// <summary>
    /// Whether this is the recommended/best match destination.
    /// </summary>
    public bool IsRecommended { get; set; }

    /// <summary>
    /// Whether this destination comes from a user-defined rule.
    /// </summary>
    public bool IsFromRule { get; set; }

    /// <summary>
    /// Confidence score (0-100) for this suggestion.
    /// </summary>
    public int Confidence { get; set; }

    /// <summary>
    /// Whether this suggestion comes from AI analysis.
    /// </summary>
    public bool IsAiSuggestion { get; set; }
}
