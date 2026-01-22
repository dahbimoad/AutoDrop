using System.Text.Json.Serialization;

namespace AutoDrop.Models;

/// <summary>
/// Settings for AI-powered file analysis features.
/// </summary>
public sealed class AiSettings
{
    /// <summary>
    /// Google Gemini API key for AI features.
    /// </summary>
    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Whether AI-powered sorting is enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    /// <summary>
    /// Whether user has accepted the AI content analysis disclaimer.
    /// AI features will not work until this is true.
    /// </summary>
    [JsonPropertyName("disclaimerAccepted")]
    public bool DisclaimerAccepted { get; set; }

    /// <summary>
    /// Minimum confidence threshold (0.0-1.0) for AI suggestions to be applied.
    /// </summary>
    [JsonPropertyName("confidenceThreshold")]
    public double ConfidenceThreshold { get; set; } = 0.7;

    /// <summary>
    /// Whether to enable AI-powered file renaming suggestions.
    /// </summary>
    [JsonPropertyName("enableSmartRename")]
    public bool EnableSmartRename { get; set; } = true;

    /// <summary>
    /// Whether to enable Vision AI for image content analysis.
    /// </summary>
    [JsonPropertyName("enableVisionAnalysis")]
    public bool EnableVisionAnalysis { get; set; } = true;

    /// <summary>
    /// Whether to enable document content analysis (PDFs, text files).
    /// </summary>
    [JsonPropertyName("enableDocumentAnalysis")]
    public bool EnableDocumentAnalysis { get; set; } = true;

    /// <summary>
    /// Maximum file size in MB for AI analysis (to avoid long processing times).
    /// </summary>
    [JsonPropertyName("maxFileSizeMb")]
    public int MaxFileSizeMb { get; set; } = 10;

    /// <summary>
    /// Checks if AI features are fully configured and ready to use.
    /// </summary>
    [JsonIgnore]
    public bool IsFullyConfigured => 
        Enabled && 
        DisclaimerAccepted && 
        !string.IsNullOrWhiteSpace(ApiKey);
}
