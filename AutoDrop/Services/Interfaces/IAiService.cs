using AutoDrop.Models;

namespace AutoDrop.Services.Interfaces;

/// <summary>
/// Main AI service that orchestrates provider selection and file analysis.
/// This service manages the active provider and delegates analysis requests.
/// </summary>
public interface IAiService
{
    /// <summary>
    /// Gets the currently active AI provider type.
    /// </summary>
    AiProvider ActiveProvider { get; }

    /// <summary>
    /// Gets information about all available providers.
    /// </summary>
    IReadOnlyList<AiProviderInfo> AvailableProviders { get; }

    /// <summary>
    /// Gets information about the currently active provider.
    /// </summary>
    AiProviderInfo? ActiveProviderInfo { get; }

    /// <summary>
    /// Whether AI features are fully configured and ready to use.
    /// Uses cached settings to avoid blocking - call RefreshAvailabilityAsync() to update.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Whether the current provider supports vision/image analysis.
    /// </summary>
    bool SupportsVision { get; }

    /// <summary>
    /// Whether the current provider supports PDF analysis.
    /// </summary>
    bool SupportsPdf { get; }

    /// <summary>
    /// Refreshes the availability check asynchronously.
    /// Call this after configuration changes to update the IsAvailable property.
    /// </summary>
    Task RefreshAvailabilityAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the active AI provider.
    /// </summary>
    Task SetActiveProviderAsync(AiProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Configures a provider with API key and model settings.
    /// </summary>
    Task ConfigureProviderAsync(AiProviderConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the specified provider's configuration.
    /// </summary>
    Task<bool> ValidateProviderAsync(AiProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes a file and returns categorization suggestions.
    /// Automatically routes to image or document analysis based on file type.
    /// Prioritizes matching to existing custom folders before suggesting new ones.
    /// </summary>
    /// <param name="filePath">The path to the file to analyze.</param>
    /// <param name="customFolders">User's custom folders to match against.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AiAnalysisResult> AnalyzeFileAsync(string filePath, IReadOnlyList<CustomFolder>? customFolders = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes an image file and returns categorization suggestions.
    /// </summary>
    /// <param name="imagePath">The path to the image file.</param>
    /// <param name="customFolders">User's custom folders to match against.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AiAnalysisResult> AnalyzeImageAsync(string imagePath, IReadOnlyList<CustomFolder>? customFolders = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes a document file and returns categorization suggestions.
    /// </summary>
    /// <param name="documentPath">The path to the document file.</param>
    /// <param name="customFolders">User's custom folders to match against.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AiAnalysisResult> AnalyzeDocumentAsync(string documentPath, IReadOnlyList<CustomFolder>? customFolders = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the configuration for a specific provider.
    /// Uses cached settings - may return null if RefreshAvailabilityAsync() hasn't been called.
    /// </summary>
    AiProviderConfig? GetProviderConfig(AiProvider provider);

    /// <summary>
    /// Gets the configuration for a specific provider asynchronously (preferred method).
    /// </summary>
    Task<AiProviderConfig?> GetProviderConfigAsync(AiProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the API key for a provider, decrypting from secure storage if needed.
    /// </summary>
    /// <param name="provider">The provider to get the API key for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The decrypted API key, or null if not configured.</returns>
    Task<string?> GetApiKeyAsync(AiProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores an API key securely using DPAPI encryption.
    /// </summary>
    /// <param name="provider">The provider to store the key for.</param>
    /// <param name="apiKey">The API key to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if stored successfully.</returns>
    Task<bool> StoreApiKeySecurelyAsync(AiProvider provider, string apiKey, CancellationToken cancellationToken = default);
}
