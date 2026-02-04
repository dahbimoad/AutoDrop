using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AutoDrop.Models;
using AutoDrop.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoDrop.Services.AI;

/// <summary>
/// Base class for AI providers with shared HTTP and parsing logic.
/// </summary>
public abstract class AiProviderBase : IAiProvider
{
    protected readonly HttpClient HttpClient;
    protected readonly ILogger Logger;
    protected AiProviderConfig? Config;
    private bool _disposed;

    protected static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"
    };

    protected static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".json", ".xml", ".csv", ".log", ".pdf"
    };

    protected AiProviderBase(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public abstract AiProvider ProviderType { get; }
    public abstract AiProviderInfo ProviderInfo { get; }
    public abstract bool SupportsVision { get; }
    public abstract bool SupportsPdf { get; }

    public virtual void Configure(AiProviderConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        Config = config;
        ConfigureHttpClient();
    }

    protected virtual void ConfigureHttpClient()
    {
        if (Config == null) return;
        HttpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", Config.ApiKey);
    }

    public abstract Task<bool> ValidateAsync(CancellationToken cancellationToken = default);
    public abstract Task<AiAnalysisResult> AnalyzeImageAsync(string imagePath, IReadOnlyList<CustomFolder>? customFolders = null, CancellationToken cancellationToken = default);
    public abstract Task<AiAnalysisResult> AnalyzeDocumentAsync(string documentPath, IReadOnlyList<CustomFolder>? customFolders = null, CancellationToken cancellationToken = default);
    public abstract Task<string> SendTextPromptAsync(string prompt, CancellationToken cancellationToken = default);

    protected async Task<string> SendRequestAsync(string url, object requestBody, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await HttpClient.PostAsync(url, content, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    protected AiAnalysisResult ParseChatCompletionResponse(string apiResponse, AiContentType contentType, IReadOnlyList<CustomFolder>? customFolders = null)
    {
        try
        {
            using var doc = JsonDocument.Parse(apiResponse);
            var root = doc.RootElement;
            var choices = root.GetProperty("choices");
            if (choices.GetArrayLength() == 0) return AiAnalysisResult.Failed("No response from AI.");
            var textContent = choices[0].GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(textContent)) return AiAnalysisResult.Failed("Empty response from AI.");
            return ParseJsonAnalysis(textContent, contentType, customFolders);
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "Failed to parse API response");
            return AiAnalysisResult.Failed("Failed to parse AI response.");
        }
    }

    protected AiAnalysisResult ParseJsonAnalysis(string textContent, AiContentType contentType, IReadOnlyList<CustomFolder>? customFolders = null)
    {
        textContent = textContent.Trim();
        if (textContent.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) textContent = textContent[7..];
        if (textContent.StartsWith("```")) textContent = textContent[3..];
        if (textContent.EndsWith("```")) textContent = textContent[..^3];
        textContent = textContent.Trim();

        try
        {
            var analysisJson = JsonDocument.Parse(textContent);
            var analysisRoot = analysisJson.RootElement;
            
            // Parse and clamp confidence to valid [0, 1] range
            var rawConfidence = analysisRoot.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0.5;
            var clampedConfidence = Math.Clamp(rawConfidence, 0.0, 1.0);
            
            var result = new AiAnalysisResult
            {
                Success = true,
                Category = analysisRoot.TryGetProperty("category", out var cat) ? cat.GetString() ?? "Unknown" : "Unknown",
                Subcategory = analysisRoot.TryGetProperty("subcategory", out var subcat) ? subcat.GetString() : null,
                SuggestedName = analysisRoot.TryGetProperty("suggestedName", out var name) ? name.GetString() : null,
                Description = analysisRoot.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                Confidence = clampedConfidence,
                ContentType = contentType,
                MatchedFolderId = analysisRoot.TryGetProperty("matchedFolderId", out var folderId) ? folderId.GetString() : null,
                SuggestedNewFolderPath = analysisRoot.TryGetProperty("suggestedNewFolderPath", out var newPath) ? newPath.GetString() : null
            };

            // Resolve matched folder details if we have a match
            if (!string.IsNullOrEmpty(result.MatchedFolderId) && customFolders != null)
            {
                var matchedFolder = customFolders.FirstOrDefault(f => 
                    f.Id.ToString().Equals(result.MatchedFolderId, StringComparison.OrdinalIgnoreCase));
                
                if (matchedFolder != null)
                {
                    result.MatchedFolderPath = matchedFolder.Path;
                    result.MatchedFolderName = matchedFolder.Name;
                    Logger.LogInformation("[AI] Matched to custom folder: {FolderName} ({FolderId})", matchedFolder.Name, matchedFolder.Id);
                }
            }

            return result;
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "Failed to parse analysis JSON: {Content}", textContent);
            return AiAnalysisResult.Failed("Failed to parse AI analysis result.");
        }
    }

    protected static string GetImageMimeType(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };

    /// <summary>
    /// Builds the custom folders section for AI prompts.
    /// </summary>
    protected static string BuildCustomFoldersSection(IReadOnlyList<CustomFolder>? customFolders)
    {
        if (customFolders == null || customFolders.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("\n\nUSER'S EXISTING FOLDERS (PRIORITIZE THESE!):");
        sb.AppendLine("If any of these folders is a good match for this file, use matchedFolderId with that folder's ID.");
        sb.AppendLine("Only suggest a new folder if NONE of these are appropriate.\n");
        
        foreach (var folder in customFolders)
        {
            sb.AppendLine($"- ID: \"{folder.Id}\" | Name: \"{folder.Name}\" | Path: \"{folder.Path}\"");
        }
        
        return sb.ToString();
    }

    protected static string BuildImagePrompt(IReadOnlyList<CustomFolder>? customFolders = null)
    {
        var customFoldersSection = BuildCustomFoldersSection(customFolders);
        var hasCustomFolders = customFolders?.Count > 0;

        return $@"Analyze this image and provide a JSON response with the following structure:
{{
    ""matchedFolderId"": ""{(hasCustomFolders ? "ID of the best matching folder from the list below, or null if none match" : "null")}"",
    ""suggestedNewFolderPath"": ""Only if no existing folder matches: suggest a path like 'Pictures/Category' or 'Documents/Category'"",
    ""category"": ""Main category (e.g., Landscapes, Portraits, Receipts, Screenshots, Documents, Artwork, Memes, Products, Food, Pets, Travel)"",
    ""subcategory"": ""More specific category if applicable"",
    ""suggestedName"": ""A descriptive filename (without extension, use underscores, max 50 chars)"",
    ""description"": ""Brief description of the image content (max 100 chars)"",
    ""confidence"": 0.85
}}{customFoldersSection}

RULES:
1. {(hasCustomFolders ? "FIRST, check if ANY of the user's existing folders above is a good match. If yes, set matchedFolderId to that folder's ID." : "No existing folders provided.")}
2. If no existing folder matches well, set matchedFolderId to null and provide suggestedNewFolderPath
3. suggestedNewFolderPath should be relative like ""Pictures/Travel"" or ""Documents/Receipts""
4. Category must be a single word or short phrase
5. suggestedName should be descriptive and file-system safe (no special characters except underscores)
6. confidence should be between 0.0 and 1.0
7. Be concise and accurate

Return ONLY valid JSON, no additional text.";
    }

    protected static string BuildDocumentPrompt(string content, IReadOnlyList<CustomFolder>? customFolders = null)
    {
        var customFoldersSection = BuildCustomFoldersSection(customFolders);
        var hasCustomFolders = customFolders?.Count > 0;

        return $@"Analyze this document content and provide a JSON response:
{{
    ""matchedFolderId"": ""{(hasCustomFolders ? "ID of the best matching folder from the list below, or null if none match" : "null")}"",
    ""suggestedNewFolderPath"": ""Only if no existing folder matches: suggest a path like 'Documents/Category'"",
    ""category"": ""Main category (e.g., Invoice, Contract, Resume, Report, Letter, Notes, Code, Configuration, Legal, Financial, Medical, Technical)"",
    ""subcategory"": ""More specific category if applicable"",
    ""suggestedName"": ""A descriptive filename based on content (without extension, use underscores, max 50 chars)"",
    ""description"": ""Brief description of the document (max 100 chars)"",
    ""confidence"": 0.85
}}{customFoldersSection}

Document content:
---
{content}
---

RULES:
1. {(hasCustomFolders ? "FIRST, check if ANY of the user's existing folders above is a good match. If yes, set matchedFolderId to that folder's ID." : "No existing folders provided.")}
2. If no existing folder matches well, set matchedFolderId to null and provide suggestedNewFolderPath
3. suggestedNewFolderPath should be relative like ""Documents/Invoices"" or ""Work/Contracts""
4. Category must be a single word or short phrase
5. suggestedName should be descriptive and file-system safe
6. confidence should be between 0.0 and 1.0
7. Be concise and accurate

Return ONLY valid JSON, no additional text.";
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing) HttpClient.Dispose();
        _disposed = true;
    }
}
