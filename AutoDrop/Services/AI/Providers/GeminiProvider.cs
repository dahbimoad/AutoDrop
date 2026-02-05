using System.Net.Http;
using System.Text;
using System.Text.Json;
using AutoDrop.Models;
using Microsoft.Extensions.Logging;

namespace AutoDrop.Services.AI.Providers;

/// <summary>
/// Google Gemini provider implementation.
/// Supports Gemini 2.5/3.0 Flash with multimodal capabilities: vision, PDF, video, audio.
/// </summary>
public sealed class GeminiProvider : AiProviderBase
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
    
    private static readonly AiProviderInfo _providerInfo = new()
    {
        Provider = AiProvider.Gemini,
        DisplayName = "Google Gemini",
        Description = "Gemini 2.5/3.0 Flash with multimodal AI: vision, documents, images, audio",
        ApiKeyUrl = "https://aistudio.google.com/apikey",
        IconGlyph = "\uE9CE", // Sparkle icon
        Models =
        [
            new() { Id = "gemini-2.5-flash", DisplayName = "Gemini 2.5 Flash", SupportsVision = true, SupportsPdf = true, MaxTokens = 1048576, Description = "Best price-performance, 1M context" },
            new() { Id = "gemini-3-flash-preview", DisplayName = "Gemini 3 Flash (Preview)", SupportsVision = true, SupportsPdf = true, MaxTokens = 1048576, Description = "Latest AI, frontier intelligence" },
            new() { Id = "gemini-2.0-flash", DisplayName = "Gemini 2.0 Flash", SupportsVision = true, SupportsPdf = true, MaxTokens = 1048576, Description = "Fast and efficient (legacy)" }
        ]
    };

    public GeminiProvider(ILogger<GeminiProvider> logger) : base(logger) { }

    public override AiProvider ProviderType => AiProvider.Gemini;
    public override AiProviderInfo ProviderInfo => _providerInfo;
    public override bool SupportsVision => true;
    public override bool SupportsPdf => true;

    protected override void ConfigureHttpClient()
    {
        // Gemini uses API key in URL, not header
    }

    private string GetApiUrl(string model) => 
        $"{BaseUrl}/{model}:generateContent?key={Config?.ApiKey}";

    public override async Task<bool> ValidateAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Config?.ApiKey)) return false;
        
        try
        {
            var model = Config.TextModel.Length > 0 ? Config.TextModel : "gemini-2.5-flash";
            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = "Hi" } } } },
                generationConfig = new { maxOutputTokens = 10 }
            };
            
            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync(GetApiUrl(model), content, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Gemini validation failed");
            return false;
        }
    }

    public override async Task<AiAnalysisResult> AnalyzeImageAsync(string imagePath, IReadOnlyList<CustomFolder>? customFolders = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        if (!File.Exists(imagePath)) return AiAnalysisResult.Failed("File not found.");
        if (string.IsNullOrWhiteSpace(Config?.ApiKey)) return AiAnalysisResult.Failed("API key not configured.");

        try
        {
            var imageBytes = await File.ReadAllBytesAsync(imagePath, ct);
            var base64 = Convert.ToBase64String(imageBytes);
            var mimeType = GetImageMimeType(Path.GetExtension(imagePath));
            var model = Config.VisionModel.Length > 0 ? Config.VisionModel : "gemini-2.5-flash";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = BuildImagePrompt(customFolders) },
                            new { inline_data = new { mime_type = mimeType, data = base64 } }
                        }
                    }
                },
                generationConfig = new { maxOutputTokens = 500, temperature = 0.2 }
            };

            var response = await SendGeminiRequestAsync(model, requestBody, ct);
            return ParseGeminiResponse(response, AiContentType.Image, customFolders);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Gemini image analysis failed");
            return AiAnalysisResult.Failed($"Analysis failed: {ex.Message}");
        }
    }

    public override async Task<AiAnalysisResult> AnalyzeDocumentAsync(string documentPath, IReadOnlyList<CustomFolder>? customFolders = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        if (!File.Exists(documentPath)) return AiAnalysisResult.Failed("File not found.");
        if (string.IsNullOrWhiteSpace(Config?.ApiKey)) return AiAnalysisResult.Failed("API key not configured.");

        try
        {
            var ext = Path.GetExtension(documentPath).ToLowerInvariant();
            var model = Config.TextModel.Length > 0 ? Config.TextModel : "gemini-2.5-flash";
            object requestBody;

            if (ext == ".pdf")
            {
                var pdfBytes = await File.ReadAllBytesAsync(documentPath, ct);
                var base64 = Convert.ToBase64String(pdfBytes);
                requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = "Analyze this PDF document and categorize it. " + BuildDocumentPrompt("", customFolders) },
                                new { inline_data = new { mime_type = "application/pdf", data = base64 } }
                            }
                        }
                    },
                    generationConfig = new { maxOutputTokens = 500, temperature = 0.2 }
                };
            }
            else
            {
                var content = await File.ReadAllTextAsync(documentPath, ct);
                if (content.Length > 10000) content = content[..10000] + "\n...[truncated]";
                requestBody = new
                {
                    contents = new[] { new { parts = new[] { new { text = BuildDocumentPrompt(content, customFolders) } } } },
                    generationConfig = new { maxOutputTokens = 500, temperature = 0.2 }
                };
            }

            var response = await SendGeminiRequestAsync(model, requestBody, ct);
            return ParseGeminiResponse(response, AiContentType.Document, customFolders);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Gemini document analysis failed");
            return AiAnalysisResult.Failed($"Analysis failed: {ex.Message}");
        }
    }

    private async Task<string> SendGeminiRequestAsync(string model, object requestBody, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await HttpClient.PostAsync(GetApiUrl(model), content, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private AiAnalysisResult ParseGeminiResponse(string apiResponse, AiContentType contentType, IReadOnlyList<CustomFolder>? customFolders = null)
    {
        try
        {
            using var doc = JsonDocument.Parse(apiResponse);
            var root = doc.RootElement;
            var candidates = root.GetProperty("candidates");
            if (candidates.GetArrayLength() == 0) return AiAnalysisResult.Failed("No response from AI.");
            
            var parts = candidates[0].GetProperty("content").GetProperty("parts");
            if (parts.GetArrayLength() == 0) return AiAnalysisResult.Failed("Empty response from AI.");
            
            var textContent = parts[0].GetProperty("text").GetString();
            if (string.IsNullOrWhiteSpace(textContent)) return AiAnalysisResult.Failed("Empty response from AI.");
            
            return ParseJsonAnalysis(textContent, contentType, customFolders);
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "Failed to parse Gemini response");
            return AiAnalysisResult.Failed("Failed to parse AI response.");
        }
    }

    public override async Task<string> SendTextPromptAsync(string prompt, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        if (string.IsNullOrWhiteSpace(Config?.ApiKey))
            throw new InvalidOperationException("API key not configured.");

        var model = Config.TextModel.Length > 0 ? Config.TextModel : "gemini-2.5-flash";
        var requestBody = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { maxOutputTokens = 100, temperature = 0.3 }
        };

        var response = await SendGeminiRequestAsync(model, requestBody, ct).ConfigureAwait(false);
        return ExtractTextFromGeminiResponse(response);
    }

    private static string ExtractTextFromGeminiResponse(string apiResponse)
    {
        using var doc = JsonDocument.Parse(apiResponse);
        var candidates = doc.RootElement.GetProperty("candidates");
        if (candidates.GetArrayLength() == 0) return string.Empty;
        var parts = candidates[0].GetProperty("content").GetProperty("parts");
        if (parts.GetArrayLength() == 0) return string.Empty;
        return parts[0].GetProperty("text").GetString() ?? string.Empty;
    }
}
