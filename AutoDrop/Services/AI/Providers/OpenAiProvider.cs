using System.Text.Json;
using AutoDrop.Models;
using Microsoft.Extensions.Logging;

namespace AutoDrop.Services.AI.Providers;

/// <summary>
/// OpenAI GPT provider implementation.
/// Supports GPT-4o (vision), GPT-4o-mini, and GPT-4-turbo.
/// </summary>
public sealed class OpenAiProvider : AiProviderBase
{
    private const string ApiUrl = "https://api.openai.com/v1/chat/completions";
    
    private static readonly AiProviderInfo _providerInfo = new()
    {
        Provider = AiProvider.OpenAI,
        DisplayName = "OpenAI",
        Description = "GPT-4o with vision, GPT-4o-mini for fast analysis",
        ApiKeyUrl = "https://platform.openai.com/api-keys",
        IconGlyph = "\uE99A", // Robot icon
        Models =
        [
            new() { Id = "gpt-4o", DisplayName = "GPT-4o", SupportsVision = true, SupportsPdf = false, MaxTokens = 128000, Description = "Most capable, vision support" },
            new() { Id = "gpt-4o-mini", DisplayName = "GPT-4o Mini", SupportsVision = true, SupportsPdf = false, MaxTokens = 128000, Description = "Fast and affordable" },
            new() { Id = "gpt-4-turbo", DisplayName = "GPT-4 Turbo", SupportsVision = true, SupportsPdf = false, MaxTokens = 128000, Description = "Previous generation" }
        ]
    };

    public OpenAiProvider(ILogger<OpenAiProvider> logger) : base(logger) { }

    public override AiProvider ProviderType => AiProvider.OpenAI;
    public override AiProviderInfo ProviderInfo => _providerInfo;
    public override bool SupportsVision => GetCurrentModel()?.SupportsVision ?? false;
    public override bool SupportsPdf => false;

    private AiModelInfo? GetCurrentModel() => 
        _providerInfo.Models.FirstOrDefault(m => m.Id == Config?.VisionModel) ?? 
        _providerInfo.Models.FirstOrDefault(m => m.Id == Config?.TextModel);

    public override async Task<bool> ValidateAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Config?.ApiKey)) return false;
        
        try
        {
            var requestBody = new
            {
                model = Config.TextModel.Length > 0 ? Config.TextModel : "gpt-4o-mini",
                messages = new[] { new { role = "user", content = "Hi" } },
                max_tokens = 5
            };
            await SendRequestAsync(ApiUrl, requestBody, ct);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "OpenAI validation failed");
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
            var model = Config.VisionModel.Length > 0 ? Config.VisionModel : "gpt-4o";

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
                            new { type = "text", text = BuildImagePrompt(customFolders) },
                            new { type = "image_url", image_url = new { url = $"data:{mimeType};base64,{base64}" } }
                        }
                    }
                },
                max_tokens = 500,
                temperature = 0.2
            };

            var response = await SendRequestAsync(ApiUrl, requestBody, ct);
            return ParseChatCompletionResponse(response, AiContentType.Image, customFolders);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "OpenAI image analysis failed");
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
            var content = await File.ReadAllTextAsync(documentPath, ct);
            if (content.Length > 5000) content = content[..5000] + "\n...[truncated]";
            var model = Config.TextModel.Length > 0 ? Config.TextModel : "gpt-4o-mini";

            var requestBody = new
            {
                model,
                messages = new[] { new { role = "user", content = BuildDocumentPrompt(content, customFolders) } },
                max_tokens = 500,
                temperature = 0.2
            };

            var response = await SendRequestAsync(ApiUrl, requestBody, ct);
            return ParseChatCompletionResponse(response, AiContentType.Document, customFolders);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "OpenAI document analysis failed");
            return AiAnalysisResult.Failed($"Analysis failed: {ex.Message}");
        }
    }

    public override async Task<string> SendTextPromptAsync(string prompt, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        if (string.IsNullOrWhiteSpace(Config?.ApiKey))
            throw new InvalidOperationException("API key not configured.");

        var model = Config.TextModel.Length > 0 ? Config.TextModel : "gpt-4o-mini";
        var requestBody = new
        {
            model,
            messages = new[] { new { role = "user", content = prompt } },
            max_tokens = 100,
            temperature = 0.3
        };

        var response = await SendRequestAsync(ApiUrl, requestBody, ct).ConfigureAwait(false);
        return ExtractTextFromChatCompletion(response);
    }

    private static string ExtractTextFromChatCompletion(string apiResponse)
    {
        using var doc = JsonDocument.Parse(apiResponse);
        var choices = doc.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0) return string.Empty;
        return choices[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }
}
