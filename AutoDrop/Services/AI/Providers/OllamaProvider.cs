using System.Net.Http;
using System.Text;
using System.Text.Json;
using AutoDrop.Models;
using Microsoft.Extensions.Logging;

namespace AutoDrop.Services.AI.Providers;

/// <summary>
/// Ollama provider implementation for local/privacy-focused AI.
/// Supports LLaVA for vision and various text models.
/// </summary>
public sealed class OllamaProvider : AiProviderBase
{
    private const string DefaultBaseUrl = "http://localhost:11434";
    
    private static readonly AiProviderInfo _providerInfo = new()
    {
        Provider = AiProvider.Ollama,
        DisplayName = "Ollama (Local)",
        Description = "Run AI locally for complete privacy - no data leaves your machine",
        ApiKeyUrl = "https://ollama.ai/download",
        IsLocal = true,
        RequiresApiKey = false,
        DefaultBaseUrl = DefaultBaseUrl,
        IconGlyph = "\uE977", // Home/Local icon
        Models =
        [
            new() { Id = "llava:13b", DisplayName = "LLaVA 13B", SupportsVision = true, SupportsPdf = false, MaxTokens = 4096, Description = "Vision-capable local model" },
            new() { Id = "llava:7b", DisplayName = "LLaVA 7B", SupportsVision = true, SupportsPdf = false, MaxTokens = 4096, Description = "Smaller vision model" },
            new() { Id = "llama3.2:latest", DisplayName = "Llama 3.2", SupportsVision = false, SupportsPdf = false, MaxTokens = 128000, Description = "Latest Llama for text" },
            new() { Id = "mistral:latest", DisplayName = "Mistral", SupportsVision = false, SupportsPdf = false, MaxTokens = 32768, Description = "Fast and efficient" },
            new() { Id = "qwen2.5:14b", DisplayName = "Qwen 2.5 14B", SupportsVision = false, SupportsPdf = false, MaxTokens = 32768, Description = "High quality text" }
        ]
    };

    public OllamaProvider(ILogger<OllamaProvider> logger) : base(logger) { }

    public override AiProvider ProviderType => AiProvider.Ollama;
    public override AiProviderInfo ProviderInfo => _providerInfo;
    public override bool SupportsVision => GetCurrentModel()?.SupportsVision ?? false;
    public override bool SupportsPdf => false;

    private string BaseUrl => Config?.BaseUrl ?? DefaultBaseUrl;
    private string ApiUrl => $"{BaseUrl}/api/chat";

    private AiModelInfo? GetCurrentModel() => 
        _providerInfo.Models.FirstOrDefault(m => m.Id == Config?.VisionModel) ?? 
        _providerInfo.Models.FirstOrDefault(m => m.Id == Config?.TextModel);

    protected override void ConfigureHttpClient()
    {
        // Ollama doesn't need auth headers
        HttpClient.DefaultRequestHeaders.Clear();
    }

    public override async Task<bool> ValidateAsync(CancellationToken ct = default)
    {
        try
        {
            // Check if Ollama is running by hitting the tags endpoint
            var response = await HttpClient.GetAsync($"{BaseUrl}/api/tags", ct);
            if (!response.IsSuccessStatusCode) return false;

            // Verify at least one model is available
            var content = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(content);
            var models = doc.RootElement.GetProperty("models");
            return models.GetArrayLength() > 0;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Ollama validation failed - is Ollama running?");
            return false;
        }
    }

    public override async Task<AiAnalysisResult> AnalyzeImageAsync(string imagePath, IReadOnlyList<CustomFolder>? customFolders = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        if (!File.Exists(imagePath)) return AiAnalysisResult.Failed("File not found.");

        try
        {
            var imageBytes = await File.ReadAllBytesAsync(imagePath, ct);
            var base64 = Convert.ToBase64String(imageBytes);
            var model = Config?.VisionModel.Length > 0 ? Config.VisionModel : "llava:13b";

            var requestBody = new
            {
                model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = BuildImagePrompt(customFolders),
                        images = new[] { base64 }
                    }
                },
                stream = false,
                options = new { temperature = 0.2 }
            };

            var response = await SendOllamaRequestAsync(requestBody, ct);
            return ParseOllamaResponse(response, AiContentType.Image, customFolders);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Ollama image analysis failed");
            return AiAnalysisResult.Failed($"Analysis failed: {ex.Message}");
        }
    }

    public override async Task<AiAnalysisResult> AnalyzeDocumentAsync(string documentPath, IReadOnlyList<CustomFolder>? customFolders = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        if (!File.Exists(documentPath)) return AiAnalysisResult.Failed("File not found.");

        try
        {
            var content = await File.ReadAllTextAsync(documentPath, ct);
            if (content.Length > 3000) content = content[..3000] + "\n...[truncated]";
            var model = Config?.TextModel.Length > 0 ? Config.TextModel : "llama3.2:latest";

            var requestBody = new
            {
                model,
                messages = new[] { new { role = "user", content = BuildDocumentPrompt(content, customFolders) } },
                stream = false,
                options = new { temperature = 0.2 }
            };

            var response = await SendOllamaRequestAsync(requestBody, ct);
            return ParseOllamaResponse(response, AiContentType.Document, customFolders);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Ollama document analysis failed");
            return AiAnalysisResult.Failed($"Analysis failed: {ex.Message}");
        }
    }

    private async Task<string> SendOllamaRequestAsync(object requestBody, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await HttpClient.PostAsync(ApiUrl, content, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private AiAnalysisResult ParseOllamaResponse(string apiResponse, AiContentType contentType, IReadOnlyList<CustomFolder>? customFolders = null)
    {
        try
        {
            using var doc = JsonDocument.Parse(apiResponse);
            var root = doc.RootElement;
            var message = root.GetProperty("message");
            var textContent = message.GetProperty("content").GetString();
            
            if (string.IsNullOrWhiteSpace(textContent))
                return AiAnalysisResult.Failed("Empty response from AI.");

            return ParseJsonAnalysis(textContent, contentType, customFolders);
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "Failed to parse Ollama response");
            return AiAnalysisResult.Failed("Failed to parse AI response.");
        }
    }

    public override async Task<string> SendTextPromptAsync(string prompt, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var model = Config?.TextModel.Length > 0 ? Config.TextModel : "llama3.2:latest";
        var requestBody = new
        {
            model,
            messages = new[] { new { role = "user", content = prompt } },
            stream = false,
            options = new { temperature = 0.3 }
        };

        var response = await SendOllamaRequestAsync(requestBody, ct).ConfigureAwait(false);
        return ExtractTextFromOllamaResponse(response);
    }

    private static string ExtractTextFromOllamaResponse(string apiResponse)
    {
        using var doc = JsonDocument.Parse(apiResponse);
        var message = doc.RootElement.GetProperty("message");
        return message.GetProperty("content").GetString() ?? string.Empty;
    }
}
