using AutoDrop.Models;

namespace AutoDrop.Services.Interfaces;

/// <summary>
/// Service for AI-powered file analysis using Google Gemini API.
/// </summary>
public interface IGeminiService
{
    /// <summary>
    /// Analyzes an image file and returns categorization suggestions.
    /// </summary>
    /// <param name="imagePath">Path to the image file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Analysis result with category and suggested name.</returns>
    Task<AiAnalysisResult> AnalyzeImageAsync(string imagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes a document file (PDF, text, etc.) and returns categorization suggestions.
    /// </summary>
    /// <param name="documentPath">Path to the document file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Analysis result with category and suggested name.</returns>
    Task<AiAnalysisResult> AnalyzeDocumentAsync(string documentPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes a file based on its type and returns appropriate suggestions.
    /// Automatically determines whether to use image or document analysis.
    /// </summary>
    /// <param name="filePath">Path to the file to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Analysis result with category and suggested name.</returns>
    Task<AiAnalysisResult> AnalyzeFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the API key is configured and working.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the API key is valid and the service is operational.</returns>
    Task<bool> ValidateApiKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if AI features are currently available (enabled, configured, and API key valid).
    /// </summary>
    bool IsAvailable { get; }
}
