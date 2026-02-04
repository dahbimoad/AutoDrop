using AutoDrop.Models;

namespace AutoDrop.Services.Interfaces;

/// <summary>
/// Contract for AI provider implementations.
/// Each provider (OpenAI, Claude, Gemini, etc.) implements this interface.
/// </summary>
public interface IAiProvider : IDisposable
{
    /// <summary>
    /// Gets the provider type.
    /// </summary>
    AiProvider ProviderType { get; }

    /// <summary>
    /// Gets metadata about this provider and its models.
    /// </summary>
    AiProviderInfo ProviderInfo { get; }

    /// <summary>
    /// Configures the provider with the given settings.
    /// </summary>
    void Configure(AiProviderConfig config);

    /// <summary>
    /// Validates the current configuration (API key, endpoint, etc.).
    /// </summary>
    Task<bool> ValidateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes an image file and returns categorization suggestions.
    /// </summary>
    /// <param name="imagePath">The path to the image file.</param>
    /// <param name="customFolders">User's custom folders to match against (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AiAnalysisResult> AnalyzeImageAsync(string imagePath, IReadOnlyList<CustomFolder>? customFolders = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes a document file and returns categorization suggestions.
    /// </summary>
    /// <param name="documentPath">The path to the document file.</param>
    /// <param name="customFolders">User's custom folders to match against (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AiAnalysisResult> AnalyzeDocumentAsync(string documentPath, IReadOnlyList<CustomFolder>? customFolders = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a text prompt and returns the AI response.
    /// </summary>
    /// <param name="prompt">The text prompt to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The AI response text.</returns>
    Task<string> SendTextPromptAsync(string prompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if vision/image analysis is available with current model.
    /// </summary>
    bool SupportsVision { get; }

    /// <summary>
    /// Checks if PDF analysis is available with current model.
    /// </summary>
    bool SupportsPdf { get; }
}
