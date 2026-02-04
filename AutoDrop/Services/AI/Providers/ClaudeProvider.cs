using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AutoDrop.Models;
using Microsoft.Extensions.Logging;

namespace AutoDrop.Services.AI.Providers;

/// <summary>
/// Anthropic Claude provider implementation.
/// Supports Claude 3.5 Sonnet (vision + PDF), Claude 3 Haiku.
/// </summary>
public sealed class ClaudeProvider : AiProviderBase
{
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion = "2023-06-01";
    
    private static readonly AiProviderInfo _providerInfo = new()
    {
        Provider = AiProvider.Claude,
        DisplayName = "Claude",
        Description = "Claude 3.5 Sonnet with vision and PDF support",
        ApiKeyUrl = "https://console.anthropic.com/settings/keys",
        IconGlyph = "\uE9D9", // Brain/Lightbulb icon
        Models =
        [
            new() { Id = "claude-3-5-sonnet-20241022", DisplayName = "Claude 3.5 Sonnet", SupportsVision = true, SupportsPdf = true, MaxTokens = 200000, Description = "Best for complex analysis" },
            new() { Id = "claude-3-5-haiku-20241022", DisplayName = "Claude 3.5 Haiku", SupportsVision = true, SupportsPdf = false, MaxTokens = 200000, Description = "Fast and efficient" },
            new() { Id = "claude-3-opus-20240229", DisplayName = "Claude 3 Opus", SupportsVision = true, SupportsPdf = true, MaxTokens = 200000, Description = "Most powerful" }
        ]
    };

    public ClaudeProvider(ILogger<ClaudeProvider> logger) : base(logger) { }

    public override AiProvider ProviderType => AiProvider.Claude;
    public override AiProviderInfo ProviderInfo => _providerInfo;
    public override bool SupportsVision => GetCurrentModel()?.SupportsVision ?? true;
    public override bool SupportsPdf => GetCurrentModel()?.SupportsPdf ?? true;

    private AiModelInfo? GetCurrentModel() => 
        _providerInfo.Models.FirstOrDefault(m => m.Id == Config?.VisionModel) ?? 
        _providerInfo.Models.FirstOrDefault(m => m.Id == Config?.TextModel);

    protected override void ConfigureHttpClient()
    {
        if (Config == null) return;
        HttpClient.DefaultRequestHeaders.Clear();
        HttpClient.DefaultRequestHeaders.Add("x-api-key", Config.ApiKey);
        HttpClient.DefaultRequestHeaders.Add("anthropic-version", ApiVersion);
    }

    public override async Task<bool> ValidateAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Config?.ApiKey)) return false;
        
        try
        {
            var requestBody = new
            {
                model = Config.TextModel.Length > 0 ? Config.TextModel : "claude-3-5-haiku-20241022",
                messages = new[] { new { role = "user", content = "Hi" } },
                max_tokens = 10
            };
            
            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync(ApiUrl, content, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Claude validation failed");
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
            var model = Config.VisionModel.Length > 0 ? Config.VisionModel : "claude-3-5-sonnet-20241022";

            var requestBody = new
            {
                model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "image", source = new { type = "base64", media_type = mimeType, data = base64 } },
                            new { type = "text", text = BuildImagePrompt(customFolders) }
                        }
                    }
                },
                max_tokens = 500
            };

            var response = await SendClaudeRequestAsync(requestBody, ct);
            return ParseClaudeResponse(response, AiContentType.Image, customFolders);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Claude image analysis failed");
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
            var model = Config.TextModel.Length > 0 ? Config.TextModel : "claude-3-5-sonnet-20241022";
            object requestBody;

            if (ext == ".pdf" && SupportsPdf)
            {
                var pdfBytes = await File.ReadAllBytesAsync(documentPath, ct);
                var base64 = Convert.ToBase64String(pdfBytes);
                requestBody = new
                {
                    model,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new { type = "document", source = new { type = "base64", media_type = "application/pdf", data = base64 } },
                                new { type = "text", text = "Analyze this PDF document and provide categorization. " + BuildDocumentPrompt("", customFolders) }
                            }
                        }
                    },
                    max_tokens = 500
                };
            }
            else
            {
                var content = await File.ReadAllTextAsync(documentPath, ct);
                if (content.Length > 5000) content = content[..5000] + "\n...[truncated]";
                requestBody = new
                {
                    model,
                    messages = new[] { new { role = "user", content = BuildDocumentPrompt(content, customFolders) } },
                    max_tokens = 500
                };
            }

            var response = await SendClaudeRequestAsync(requestBody, ct);
            return ParseClaudeResponse(response, AiContentType.Document, customFolders);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Claude document analysis failed");
            return AiAnalysisResult.Failed($"Analysis failed: {ex.Message}");
        }
    }

    private async Task<string> SendClaudeRequestAsync(object requestBody, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await HttpClient.PostAsync(ApiUrl, content, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private AiAnalysisResult ParseClaudeResponse(string apiResponse, AiContentType contentType, IReadOnlyList<CustomFolder>? customFolders = null)
    {
        try
        {
            using var doc = JsonDocument.Parse(apiResponse);
            var root = doc.RootElement;
            var contentArray = root.GetProperty("content");
            if (contentArray.GetArrayLength() == 0) return AiAnalysisResult.Failed("No response from AI.");
            var textContent = contentArray[0].GetProperty("text").GetString();
            if (string.IsNullOrWhiteSpace(textContent)) return AiAnalysisResult.Failed("Empty response from AI.");
            return ParseJsonAnalysis(textContent, contentType, customFolders);
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "Failed to parse Claude response");
            return AiAnalysisResult.Failed("Failed to parse AI response.");
        }
    }

    public override async Task<string> SendTextPromptAsync(string prompt, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        if (string.IsNullOrWhiteSpace(Config?.ApiKey))
            throw new InvalidOperationException("API key not configured.");

        var model = Config.TextModel.Length > 0 ? Config.TextModel : "claude-3-5-haiku-20241022";
        var requestBody = new
        {
            model,
            messages = new[] { new { role = "user", content = prompt } },
            max_tokens = 100
        };

        var response = await SendClaudeRequestAsync(requestBody, ct).ConfigureAwait(false);
        return ExtractTextFromClaudeResponse(response);
    }

    private static string ExtractTextFromClaudeResponse(string apiResponse)
    {
        using var doc = JsonDocument.Parse(apiResponse);
        var contentArray = doc.RootElement.GetProperty("content");
        if (contentArray.GetArrayLength() == 0) return string.Empty;
        return contentArray[0].GetProperty("text").GetString() ?? string.Empty;
    }
}
