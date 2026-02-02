using AutoDrop.Models;
using Microsoft.Extensions.Logging;

namespace AutoDrop.Services.AI.Providers;

/// <summary>
/// Groq provider implementation (fast inference).
/// Supports Llama 3.3 70B and Llama 3.2 Vision models.
/// </summary>
public sealed class GroqProvider : AiProviderBase
{
    private const string ApiUrl = "https://api.groq.com/openai/v1/chat/completions";
    
    private static readonly AiProviderInfo _providerInfo = new()
    {
        Provider = AiProvider.Groq,
        DisplayName = "Groq",
        Description = "Ultra-fast Llama models with vision support",
        ApiKeyUrl = "https://console.groq.com/keys",
        IconGlyph = "\uE945", // Lightning bolt icon
        Models =
        [
            new() { Id = "llama-3.3-70b-versatile", DisplayName = "Llama 3.3 70B", SupportsVision = false, SupportsPdf = false, MaxTokens = 128000, Description = "Best quality text analysis" },
            new() { Id = "llama-3.2-90b-vision-preview", DisplayName = "Llama 3.2 90B Vision", SupportsVision = true, SupportsPdf = false, MaxTokens = 128000, Description = "Vision-capable" },
            new() { Id = "llama-3.1-70b-versatile", DisplayName = "Llama 3.1 70B", SupportsVision = false, SupportsPdf = false, MaxTokens = 128000, Description = "Fast and reliable" },
            new() { Id = "mixtral-8x7b-32768", DisplayName = "Mixtral 8x7B", SupportsVision = false, SupportsPdf = false, MaxTokens = 32768, Description = "Efficient MoE model" }
        ]
    };

    public GroqProvider(ILogger<GroqProvider> logger) : base(logger) { }

    public override AiProvider ProviderType => AiProvider.Groq;
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
                model = Config.TextModel.Length > 0 ? Config.TextModel : "llama-3.3-70b-versatile",
                messages = new[] { new { role = "user", content = "Hi" } },
                max_tokens = 5
            };
            await SendRequestAsync(ApiUrl, requestBody, ct);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Groq validation failed");
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
            var model = Config.VisionModel.Length > 0 ? Config.VisionModel : "llama-3.2-90b-vision-preview";

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
            Logger.LogError(ex, "Groq image analysis failed");
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
            var model = Config.TextModel.Length > 0 ? Config.TextModel : "llama-3.3-70b-versatile";

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
            Logger.LogError(ex, "Groq document analysis failed");
            return AiAnalysisResult.Failed($"Analysis failed: {ex.Message}");
        }
    }
}
