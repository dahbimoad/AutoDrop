using System.Text.Json.Serialization;

namespace AutoDrop.Models;

/// <summary>
/// Configuration for a specific AI provider.
/// </summary>
public sealed class AiProviderConfig
{
    /// <summary>
    /// The AI provider type.
    /// </summary>
    [JsonPropertyName("provider")]
    public AiProvider Provider { get; set; }

    /// <summary>
    /// API key for cloud providers.
    /// NOTE: This field is deprecated for security. New API keys are stored
    /// securely via ICredentialService. This field remains for migration
    /// of existing keys and for local providers that don't need encryption.
    /// </summary>
    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Whether the API key is stored securely (via DPAPI/Credential Manager).
    /// When true, the ApiKey field is empty and the key is retrieved from secure storage.
    /// </summary>
    [JsonPropertyName("isKeySecured")]
    public bool IsKeySecured { get; set; }

    /// <summary>
    /// Base URL for API calls (useful for Ollama custom endpoints).
    /// </summary>
    [JsonPropertyName("baseUrl")]
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Selected model for text/document analysis.
    /// </summary>
    [JsonPropertyName("textModel")]
    public string TextModel { get; set; } = string.Empty;

    /// <summary>
    /// Selected model for vision/image analysis.
    /// </summary>
    [JsonPropertyName("visionModel")]
    public string VisionModel { get; set; } = string.Empty;

    /// <summary>
    /// Whether the API key has been validated.
    /// </summary>
    [JsonPropertyName("isValidated")]
    public bool IsValidated { get; set; }

    /// <summary>
    /// Last validation timestamp.
    /// </summary>
    [JsonPropertyName("lastValidated")]
    public DateTime? LastValidated { get; set; }

    /// <summary>
    /// Gets the credential key used for secure storage.
    /// </summary>
    [JsonIgnore]
    public string CredentialKey => $"AutoDrop_{Provider}_ApiKey";
}

/// <summary>
/// Information about an AI model's capabilities.
/// </summary>
public sealed record AiModelInfo
{
    /// <summary>
    /// Model identifier used in API calls.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name for the UI.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Whether the model supports image/vision analysis.
    /// </summary>
    public bool SupportsVision { get; init; }

    /// <summary>
    /// Whether the model supports PDF document analysis.
    /// </summary>
    public bool SupportsPdf { get; init; }

    /// <summary>
    /// Maximum context window (tokens).
    /// </summary>
    public int MaxTokens { get; init; }

    /// <summary>
    /// Brief description of the model.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Provider metadata including available models and capabilities.
/// </summary>
public sealed record AiProviderInfo
{
    /// <summary>
    /// Provider identifier.
    /// </summary>
    public required AiProvider Provider { get; init; }

    /// <summary>
    /// Display name for the UI.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Provider description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// URL to get an API key.
    /// </summary>
    public string? ApiKeyUrl { get; init; }

    /// <summary>
    /// Whether this is a local/privacy-focused provider.
    /// </summary>
    public bool IsLocal { get; init; }

    /// <summary>
    /// Whether an API key is required.
    /// </summary>
    public bool RequiresApiKey { get; init; } = true;

    /// <summary>
    /// Default base URL for API calls.
    /// </summary>
    public string? DefaultBaseUrl { get; init; }

    /// <summary>
    /// Available models for this provider.
    /// </summary>
    public required IReadOnlyList<AiModelInfo> Models { get; init; }

    /// <summary>
    /// Icon glyph from Segoe Fluent Icons font for visual identification.
    /// Common glyphs: E99A (Robot), E9D9 (Lightbulb), E945 (Sparkle), E945 (Lightning), E977 (Home)
    /// </summary>
    public string IconGlyph { get; init; } = "\uE99A";
}
