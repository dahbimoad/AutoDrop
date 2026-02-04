using System.Text.Json.Serialization;

namespace AutoDrop.Models;

/// <summary>
/// Settings for AI-powered file analysis features.
/// Supports multiple AI providers (OpenAI, Claude, Gemini, Groq, Local).
/// </summary>
public sealed class AiSettings
{
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
    /// The currently active AI provider.
    /// Defaults to Local for best out-of-box experience.
    /// </summary>
    [JsonPropertyName("activeProvider")]
    public AiProvider ActiveProvider { get; set; } = AiProvider.Local;

    /// <summary>
    /// Configuration for each AI provider (API keys, models, etc.).
    /// </summary>
    [JsonPropertyName("providerConfigs")]
    public List<AiProviderConfig> ProviderConfigs { get; set; } = [];

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
    /// Default base path for AI-suggested new folders.
    /// If empty, uses user's Documents folder.
    /// </summary>
    [JsonPropertyName("defaultNewFolderBasePath")]
    public string DefaultNewFolderBasePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets the resolved base path for new folders.
    /// Returns configured path or Documents folder as fallback.
    /// </summary>
    [JsonIgnore]
    public string ResolvedNewFolderBasePath =>
        !string.IsNullOrWhiteSpace(DefaultNewFolderBasePath) && Directory.Exists(DefaultNewFolderBasePath)
            ? DefaultNewFolderBasePath
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    /// <summary>
    /// Checks if AI features are fully configured and ready to use.
    /// </summary>
    [JsonIgnore]
    public bool IsFullyConfigured
    {
        get
        {
            if (!Enabled || !DisclaimerAccepted) return false;
            var activeConfig = ProviderConfigs.FirstOrDefault(c => c.Provider == ActiveProvider);
            if (activeConfig == null) return false;
            
            // Local AI doesn't need API key (just needs models downloaded)
            if (ActiveProvider == AiProvider.Local) return true;
            
            return !string.IsNullOrWhiteSpace(activeConfig.ApiKey) || activeConfig.IsKeySecured;
        }
    }
}
