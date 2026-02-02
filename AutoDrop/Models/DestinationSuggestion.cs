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

    /// <summary>
    /// Whether this AI suggestion creates a new folder (vs matching existing).
    /// When true, user can browse to change location before moving.
    /// </summary>
    public bool IsNewFolder { get; set; }

    /// <summary>
    /// The suggested folder name for new folders (without base path).
    /// </summary>
    public string? SuggestedFolderName { get; set; }
}
