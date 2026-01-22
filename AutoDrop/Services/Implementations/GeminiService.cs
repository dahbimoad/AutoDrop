using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AutoDrop.Models;
using AutoDrop.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoDrop.Services.Implementations;

/// <summary>
/// Implementation of AI-powered file analysis using Groq API (OpenAI-compatible).
/// Uses Llama 3.3 70B for optimal quality.
/// </summary>
public sealed class GeminiService : IGeminiService, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<GeminiService> _logger;
    private readonly HttpClient _httpClient;
    private bool _disposed;

    private const string GroqApiBaseUrl = "https://api.groq.com/openai/v1/chat/completions";
    private const string DefaultModel = "llama-3.3-70b-versatile"; // Best quality model on Groq
    private const string VisionModel = "llama-3.2-90b-vision-preview"; // Vision-capable model
    
    // Supported image extensions for Vision AI
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"
    };
    
    // Supported document extensions for text analysis
    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".json", ".xml", ".csv", ".log"
    };

    public GeminiService(ISettingsService settingsService, ILogger<GeminiService> logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        _logger.LogDebug("GeminiService initialized (using Groq API)");
    }

    /// <inheritdoc />
    public bool IsAvailable => GetAiSettingsAsync().GetAwaiter().GetResult()?.IsFullyConfigured ?? false;

    /// <inheritdoc />
    public async Task<AiAnalysisResult> AnalyzeFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath);
        
        _logger.LogInformation("[AI] Starting file analysis: {FileName} (Type: {Extension})", fileName, extension);
        
        if (ImageExtensions.Contains(extension))
        {
            _logger.LogDebug("[AI] Routing to image analysis for: {FileName}", fileName);
            return await AnalyzeImageAsync(filePath, cancellationToken).ConfigureAwait(false);
        }
        
        if (DocumentExtensions.Contains(extension))
        {
            _logger.LogDebug("[AI] Routing to document analysis for: {FileName}", fileName);
            return await AnalyzeDocumentAsync(filePath, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogWarning("[AI] Unsupported file type for AI analysis: {Extension} (File: {FileName})", extension, fileName);
        return AiAnalysisResult.Failed($"File type '{extension}' is not supported for AI analysis.");
    }

    /// <inheritdoc />
    public async Task<AiAnalysisResult> AnalyzeImageAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        
        var fileName = Path.GetFileName(imagePath);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var settings = await GetAiSettingsAsync().ConfigureAwait(false);
        if (!ValidateSettings(settings, out var error))
        {
            _logger.LogWarning("[AI] Image analysis blocked - settings validation failed: {Error}", error);
            return AiAnalysisResult.Failed(error);
        }

        if (!File.Exists(imagePath))
        {
            _logger.LogWarning("[AI] Image file not found: {Path}", imagePath);
            return AiAnalysisResult.Failed("Image file not found.");
        }

        try
        {
            _logger.LogInformation("[AI] 👁️ Vision Drop: Analyzing image '{FileName}'", fileName);
            
            // Read and encode image
            var imageBytes = await File.ReadAllBytesAsync(imagePath, cancellationToken).ConfigureAwait(false);
            var fileSizeMb = imageBytes.Length / (1024.0 * 1024.0);
            
            _logger.LogDebug("[AI] Image loaded: {SizeMb:F2} MB", fileSizeMb);
            
            // Check file size
            if (imageBytes.Length > settings!.MaxFileSizeMb * 1024 * 1024)
            {
                _logger.LogWarning("[AI] Image too large: {SizeMb:F2} MB > {MaxSize} MB limit", fileSizeMb, settings.MaxFileSizeMb);
                return AiAnalysisResult.Failed($"Image exceeds maximum size of {settings.MaxFileSizeMb}MB.");
            }

            var base64Image = Convert.ToBase64String(imageBytes);
            var mimeType = GetMimeType(Path.GetExtension(imagePath));
            
            _logger.LogDebug("[AI] Sending image to Groq API (MIME: {MimeType})", mimeType);

            var prompt = BuildImageAnalysisPrompt();
            var response = await SendVisionRequestAsync(settings.ApiKey, base64Image, mimeType, prompt, cancellationToken).ConfigureAwait(false);
            
            stopwatch.Stop();
            var result = ParseAnalysisResponse(response, AiContentType.Image);
            
            if (result.Success)
            {
                _logger.LogInformation("[AI] ✅ Image analysis complete: Category='{Category}', Confidence={Confidence:P0}, SuggestedName='{Name}' ({ElapsedMs}ms)", 
                    result.Category, result.Confidence, result.SuggestedName ?? "N/A", stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogWarning("[AI] ❌ Image analysis failed: {Error} ({ElapsedMs}ms)", result.Error, stopwatch.ElapsedMilliseconds);
            }
            
            return result;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[AI] ❌ API request failed for image '{FileName}' ({ElapsedMs}ms): {Message}", fileName, stopwatch.ElapsedMilliseconds, ex.Message);
            return AiAnalysisResult.Failed($"API request failed: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning("[AI] ⏱️ Image analysis timed out for '{FileName}' ({ElapsedMs}ms)", fileName, stopwatch.ElapsedMilliseconds);
            return AiAnalysisResult.Failed("Analysis timed out. Please try again.");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[AI] ❌ Unexpected error during image analysis for '{FileName}' ({ElapsedMs}ms)", fileName, stopwatch.ElapsedMilliseconds);
            return AiAnalysisResult.Failed($"Analysis failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<AiAnalysisResult> AnalyzeDocumentAsync(string documentPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        
        var fileName = Path.GetFileName(documentPath);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var settings = await GetAiSettingsAsync().ConfigureAwait(false);
        if (!ValidateSettings(settings, out var error))
        {
            _logger.LogWarning("[AI] Document analysis blocked - settings validation failed: {Error}", error);
            return AiAnalysisResult.Failed(error);
        }

        if (!File.Exists(documentPath))
        {
            _logger.LogWarning("[AI] Document file not found: {Path}", documentPath);
            return AiAnalysisResult.Failed("Document file not found.");
        }

        try
        {
            _logger.LogInformation("[AI] 🧠 Smart Context: Analyzing document '{FileName}'", fileName);
            
            // Read document content (first 5000 chars to avoid token limits)
            var content = await File.ReadAllTextAsync(documentPath, cancellationToken).ConfigureAwait(false);
            var originalLength = content.Length;
            
            if (content.Length > 5000)
            {
                content = content[..5000] + "\n... [truncated]";
                _logger.LogDebug("[AI] Document truncated: {OriginalChars} chars -> 5000 chars", originalLength);
            }
            else
            {
                _logger.LogDebug("[AI] Document loaded: {CharCount} chars", content.Length);
            }

            _logger.LogDebug("[AI] Sending document to Groq API");
            
            var prompt = BuildDocumentAnalysisPrompt(content);
            var response = await SendTextRequestAsync(settings!.ApiKey, prompt, cancellationToken).ConfigureAwait(false);
            
            stopwatch.Stop();
            var result = ParseAnalysisResponse(response, AiContentType.Document);
            
            if (result.Success)
            {
                _logger.LogInformation("[AI] ✅ Document analysis complete: Category='{Category}', Confidence={Confidence:P0}, SuggestedName='{Name}' ({ElapsedMs}ms)", 
                    result.Category, result.Confidence, result.SuggestedName ?? "N/A", stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogWarning("[AI] ❌ Document analysis failed: {Error} ({ElapsedMs}ms)", result.Error, stopwatch.ElapsedMilliseconds);
            }
            
            return result;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[AI] ❌ API request failed for document '{FileName}' ({ElapsedMs}ms): {Message}", fileName, stopwatch.ElapsedMilliseconds, ex.Message);
            return AiAnalysisResult.Failed($"API request failed: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning("[AI] ⏱️ Document analysis timed out for '{FileName}' ({ElapsedMs}ms)", fileName, stopwatch.ElapsedMilliseconds);
            return AiAnalysisResult.Failed("Analysis timed out. Please try again.");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[AI] ❌ Unexpected error during document analysis for '{FileName}' ({ElapsedMs}ms)", fileName, stopwatch.ElapsedMilliseconds);
            return AiAnalysisResult.Failed($"Analysis failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<bool> ValidateApiKeyAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[AI] 🔑 Validating API key...");
        
        var settings = await GetAiSettingsAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(settings?.ApiKey))
        {
            _logger.LogWarning("[AI] API key validation failed: No API key configured");
            return false;
        }

        try
        {
            // Simple validation with a minimal request to Groq
            var requestBody = new
            {
                model = DefaultModel,
                messages = new[]
                {
                    new { role = "user", content = "Hi" }
                },
                max_tokens = 5
            };

            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            using var request = new HttpRequestMessage(HttpMethod.Post, GroqApiBaseUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
            request.Content = content;
            
            _logger.LogDebug("[AI] Sending validation request to Groq API");
            var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[AI] ✅ API key validated successfully");
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogWarning("[AI] ❌ API key validation failed: HTTP {StatusCode} - {Error}", (int)response.StatusCode, errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AI] ❌ API key validation failed: {Message}", ex.Message);
            return false;
        }
    }

    #region Private Methods

    private async Task<AiSettings?> GetAiSettingsAsync()
    {
        var settings = await _settingsService.GetSettingsAsync().ConfigureAwait(false);
        return settings.AiSettings;
    }

    private bool ValidateSettings(AiSettings? settings, out string error)
    {
        if (settings == null)
        {
            error = "AI settings not configured.";
            return false;
        }

        if (!settings.Enabled)
        {
            error = "AI features are disabled.";
            return false;
        }

        if (!settings.DisclaimerAccepted)
        {
            error = "AI disclaimer not accepted. Please accept the AI terms in Settings.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            error = "API key not configured. Please add your Groq API key in Settings.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string BuildImageAnalysisPrompt()
    {
        return """
            Analyze this image and provide a JSON response with the following structure:
            {
                "category": "Main category (e.g., Landscapes, Portraits, Receipts, Screenshots, Documents, Artwork, Memes, Products, Food, Pets, Travel)",
                "subcategory": "More specific category if applicable",
                "suggestedName": "A descriptive filename (without extension, use underscores, max 50 chars)",
                "description": "Brief description of the image content (max 100 chars)",
                "confidence": 0.85
            }
            
            Rules:
            - Category must be a single word or short phrase
            - suggestedName should be descriptive and file-system safe (no special characters except underscores)
            - confidence should be between 0.0 and 1.0
            - Be concise and accurate
            
            Return ONLY valid JSON, no additional text.
            """;
    }

    private static string BuildDocumentAnalysisPrompt(string content)
    {
        return @"Analyze this document content and provide a JSON response with the following structure:
{
    ""category"": ""Main category (e.g., Invoice, Contract, Resume, Report, Letter, Notes, Code, Configuration, Legal, Financial, Medical, Technical)"",
    ""subcategory"": ""More specific category if applicable"",
    ""suggestedName"": ""A descriptive filename based on content (without extension, use underscores, max 50 chars)"",
    ""description"": ""Brief description of the document (max 100 chars)"",
    ""confidence"": 0.85
}

Document content:
---
" + content + @"
---

Rules:
- Category must be a single word or short phrase
- suggestedName should be descriptive and file-system safe
- confidence should be between 0.0 and 1.0
- Be concise and accurate

Return ONLY valid JSON, no additional text.";
    }

    private async Task<string> SendVisionRequestAsync(string apiKey, string base64Image, string mimeType, string prompt, CancellationToken cancellationToken)
    {
        // Groq vision format uses image_url with base64 data URL
        var requestBody = new
        {
            model = VisionModel,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt },
                        new 
                        { 
                            type = "image_url", 
                            image_url = new 
                            { 
                                url = $"data:{mimeType};base64,{base64Image}" 
                            } 
                        }
                    }
                }
            },
            temperature = 0.2,
            max_tokens = 500
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        using var request = new HttpRequestMessage(HttpMethod.Post, GroqApiBaseUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = content;
        
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> SendTextRequestAsync(string apiKey, string prompt, CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = DefaultModel,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.2,
            max_tokens = 500
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        using var request = new HttpRequestMessage(HttpMethod.Post, GroqApiBaseUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = content;
        
        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private AiAnalysisResult ParseAnalysisResponse(string apiResponse, AiContentType contentType)
    {
        try
        {
            using var doc = JsonDocument.Parse(apiResponse);
            var root = doc.RootElement;
            
            // Navigate to the text content in OpenAI-compatible response format
            var choices = root.GetProperty("choices");
            if (choices.GetArrayLength() == 0)
            {
                return AiAnalysisResult.Failed("No response from AI.");
            }

            var textContent = choices[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(textContent))
            {
                return AiAnalysisResult.Failed("Empty response from AI.");
            }

            // Clean up the response (remove markdown code blocks if present)
            textContent = textContent.Trim();
            if (textContent.StartsWith("```json"))
            {
                textContent = textContent[7..];
            }
            if (textContent.StartsWith("```"))
            {
                textContent = textContent[3..];
            }
            if (textContent.EndsWith("```"))
            {
                textContent = textContent[..^3];
            }
            textContent = textContent.Trim();

            // Parse the JSON response
            var analysisJson = JsonDocument.Parse(textContent);
            var analysisRoot = analysisJson.RootElement;

            return new AiAnalysisResult
            {
                Success = true,
                Category = analysisRoot.TryGetProperty("category", out var cat) ? cat.GetString() ?? "Unknown" : "Unknown",
                Subcategory = analysisRoot.TryGetProperty("subcategory", out var subcat) ? subcat.GetString() : null,
                SuggestedName = analysisRoot.TryGetProperty("suggestedName", out var name) ? name.GetString() : null,
                Description = analysisRoot.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                Confidence = analysisRoot.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0.5,
                ContentType = contentType
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI response");
            return AiAnalysisResult.Failed("Failed to parse AI response.");
        }
    }

    private static string GetMimeType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        
        _httpClient.Dispose();
        _disposed = true;
        _logger.LogDebug("GeminiService disposed");
    }
}
