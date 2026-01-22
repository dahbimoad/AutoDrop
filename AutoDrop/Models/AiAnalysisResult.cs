using System.Text.Json.Serialization;

namespace AutoDrop.Models;

/// <summary>
/// Result of an AI file analysis operation.
/// </summary>
public sealed class AiAnalysisResult
{
    /// <summary>
    /// Whether the analysis was successful.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Suggested category for the file (e.g., "Receipts", "Landscapes", "Contracts").
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Suggested subcategory for more specific organization.
    /// </summary>
    [JsonPropertyName("subcategory")]
    public string? Subcategory { get; set; }

    /// <summary>
    /// AI-suggested filename (without extension).
    /// </summary>
    [JsonPropertyName("suggestedName")]
    public string? SuggestedName { get; set; }

    /// <summary>
    /// Brief description of the file content.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Confidence score (0.0-1.0) of the AI analysis.
    /// </summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    /// <summary>
    /// Error message if analysis failed.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Type of content detected (Image, Document, Code, etc.).
    /// </summary>
    [JsonPropertyName("contentType")]
    public AiContentType ContentType { get; set; }

    /// <summary>
    /// Creates a failed result with an error message.
    /// </summary>
    public static AiAnalysisResult Failed(string error) => new()
    {
        Success = false,
        Error = error
    };

    /// <summary>
    /// Creates a successful result with category and confidence.
    /// </summary>
    public static AiAnalysisResult Successful(string category, double confidence, AiContentType contentType) => new()
    {
        Success = true,
        Category = category,
        Confidence = confidence,
        ContentType = contentType
    };
}

/// <summary>
/// Type of content detected by AI analysis.
/// </summary>
public enum AiContentType
{
    Unknown,
    Image,
    Document,
    Code,
    Archive,
    Media
}
