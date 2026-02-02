using AutoDrop.Models;
using AutoDrop.Services.AI.Providers;
using AutoDrop.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoDrop.Tests.Services.AI;

/// <summary>
/// Unit tests for individual AI provider implementations.
/// Tests provider info, configuration, and capability detection.
/// </summary>
public sealed class AiProviderTests : IDisposable
{
    private readonly TestFileFixture _fixture;

    public AiProviderTests()
    {
        _fixture = new TestFileFixture();
    }

    public void Dispose() => _fixture.Dispose();

    #region OpenAI Provider Tests

    [Fact]
    public void OpenAiProvider_ProviderType_IsOpenAI()
    {
        // Arrange
        using var provider = new OpenAiProvider(NullLogger<OpenAiProvider>.Instance);

        // Assert
        provider.ProviderType.Should().Be(AiProvider.OpenAI);
    }

    [Fact]
    public void OpenAiProvider_ProviderInfo_HasCorrectMetadata()
    {
        // Arrange
        using var provider = new OpenAiProvider(NullLogger<OpenAiProvider>.Instance);

        // Assert
        provider.ProviderInfo.DisplayName.Should().Contain("OpenAI");
        provider.ProviderInfo.RequiresApiKey.Should().BeTrue();
        provider.ProviderInfo.IsLocal.Should().BeFalse();
        provider.ProviderInfo.Models.Should().NotBeEmpty();
        provider.ProviderInfo.ApiKeyUrl.Should().Contain("openai");
    }

    [Fact]
    public void OpenAiProvider_Models_IncludeVisionCapable()
    {
        // Arrange
        using var provider = new OpenAiProvider(NullLogger<OpenAiProvider>.Instance);

        // Assert
        provider.ProviderInfo.Models.Should().Contain(m => m.SupportsVision);
    }

    [Fact]
    public void OpenAiProvider_Configure_SetsVisionSupport()
    {
        // Arrange
        using var provider = new OpenAiProvider(NullLogger<OpenAiProvider>.Instance);
        var config = new AiProviderConfig
        {
            Provider = AiProvider.OpenAI,
            ApiKey = "test-key",
            VisionModel = "gpt-4o"
        };

        // Act
        provider.Configure(config);

        // Assert
        provider.SupportsVision.Should().BeTrue();
    }

    #endregion

    #region Claude Provider Tests

    [Fact]
    public void ClaudeProvider_ProviderType_IsClaude()
    {
        // Arrange
        using var provider = new ClaudeProvider(NullLogger<ClaudeProvider>.Instance);

        // Assert
        provider.ProviderType.Should().Be(AiProvider.Claude);
    }

    [Fact]
    public void ClaudeProvider_ProviderInfo_HasCorrectMetadata()
    {
        // Arrange
        using var provider = new ClaudeProvider(NullLogger<ClaudeProvider>.Instance);

        // Assert
        provider.ProviderInfo.DisplayName.Should().Contain("Claude");
        provider.ProviderInfo.RequiresApiKey.Should().BeTrue();
        provider.ProviderInfo.IsLocal.Should().BeFalse();
        provider.ProviderInfo.Models.Should().NotBeEmpty();
    }

    [Fact]
    public void ClaudeProvider_Models_IncludePdfSupport()
    {
        // Arrange
        using var provider = new ClaudeProvider(NullLogger<ClaudeProvider>.Instance);

        // Assert
        provider.ProviderInfo.Models.Should().Contain(m => m.SupportsPdf);
    }

    [Fact]
    public void ClaudeProvider_Configure_SetsPdfSupport()
    {
        // Arrange
        using var provider = new ClaudeProvider(NullLogger<ClaudeProvider>.Instance);
        var config = new AiProviderConfig
        {
            Provider = AiProvider.Claude,
            ApiKey = "test-key",
            VisionModel = "claude-3-5-sonnet-20241022"
        };

        // Act
        provider.Configure(config);

        // Assert
        provider.SupportsPdf.Should().BeTrue();
    }

    #endregion

    #region Gemini Provider Tests

    [Fact]
    public void GeminiProvider_ProviderType_IsGemini()
    {
        // Arrange
        using var provider = new GeminiProvider(NullLogger<GeminiProvider>.Instance);

        // Assert
        provider.ProviderType.Should().Be(AiProvider.Gemini);
    }

    [Fact]
    public void GeminiProvider_ProviderInfo_HasCorrectMetadata()
    {
        // Arrange
        using var provider = new GeminiProvider(NullLogger<GeminiProvider>.Instance);

        // Assert
        provider.ProviderInfo.DisplayName.Should().Contain("Gemini");
        provider.ProviderInfo.RequiresApiKey.Should().BeTrue();
        provider.ProviderInfo.IsLocal.Should().BeFalse();
        provider.ProviderInfo.Models.Should().NotBeEmpty();
    }

    [Fact]
    public void GeminiProvider_Models_SupportVisionAndPdf()
    {
        // Arrange
        using var provider = new GeminiProvider(NullLogger<GeminiProvider>.Instance);

        // Assert
        provider.ProviderInfo.Models.Should().Contain(m => m.SupportsVision);
        provider.ProviderInfo.Models.Should().Contain(m => m.SupportsPdf);
    }

    #endregion

    #region Groq Provider Tests

    [Fact]
    public void GroqProvider_ProviderType_IsGroq()
    {
        // Arrange
        using var provider = new GroqProvider(NullLogger<GroqProvider>.Instance);

        // Assert
        provider.ProviderType.Should().Be(AiProvider.Groq);
    }

    [Fact]
    public void GroqProvider_ProviderInfo_HasCorrectMetadata()
    {
        // Arrange
        using var provider = new GroqProvider(NullLogger<GroqProvider>.Instance);

        // Assert
        provider.ProviderInfo.DisplayName.Should().Contain("Groq");
        provider.ProviderInfo.RequiresApiKey.Should().BeTrue();
        provider.ProviderInfo.IsLocal.Should().BeFalse();
        provider.ProviderInfo.Models.Should().NotBeEmpty();
    }

    [Fact]
    public void GroqProvider_Models_IncludeVisionModel()
    {
        // Arrange
        using var provider = new GroqProvider(NullLogger<GroqProvider>.Instance);

        // Assert
        provider.ProviderInfo.Models.Should().Contain(m => m.SupportsVision);
    }

    #endregion

    #region Ollama Provider Tests

    [Fact]
    public void OllamaProvider_ProviderType_IsOllama()
    {
        // Arrange
        using var provider = new OllamaProvider(NullLogger<OllamaProvider>.Instance);

        // Assert
        provider.ProviderType.Should().Be(AiProvider.Ollama);
    }

    [Fact]
    public void OllamaProvider_ProviderInfo_IsLocal()
    {
        // Arrange
        using var provider = new OllamaProvider(NullLogger<OllamaProvider>.Instance);

        // Assert
        provider.ProviderInfo.IsLocal.Should().BeTrue();
        provider.ProviderInfo.RequiresApiKey.Should().BeFalse();
    }

    [Fact]
    public void OllamaProvider_ProviderInfo_HasDefaultBaseUrl()
    {
        // Arrange
        using var provider = new OllamaProvider(NullLogger<OllamaProvider>.Instance);

        // Assert
        provider.ProviderInfo.DefaultBaseUrl.Should().Contain("localhost:11434");
    }

    [Fact]
    public void OllamaProvider_Configure_UsesCustomBaseUrl()
    {
        // Arrange
        using var provider = new OllamaProvider(NullLogger<OllamaProvider>.Instance);
        var config = new AiProviderConfig
        {
            Provider = AiProvider.Ollama,
            BaseUrl = "http://custom-host:11434",
            VisionModel = "llava"
        };

        // Act
        provider.Configure(config);

        // Assert - No exception thrown
        provider.ProviderType.Should().Be(AiProvider.Ollama);
    }

    [Fact]
    public void OllamaProvider_Models_IncludeVisionModel()
    {
        // Arrange
        using var provider = new OllamaProvider(NullLogger<OllamaProvider>.Instance);

        // Assert
        provider.ProviderInfo.Models.Should().Contain(m => m.SupportsVision);
        provider.ProviderInfo.Models.Should().Contain(m => m.Id.Contains("llava", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Common Provider Tests

    [Fact]
    public void AllProviders_OpenAi_HasNonEmptyModels()
    {
        using var provider = new OpenAiProvider(NullLogger<OpenAiProvider>.Instance);
        provider.ProviderInfo.Models.Should().NotBeEmpty();
    }

    [Fact]
    public void AllProviders_Claude_HasNonEmptyModels()
    {
        using var provider = new ClaudeProvider(NullLogger<ClaudeProvider>.Instance);
        provider.ProviderInfo.Models.Should().NotBeEmpty();
    }

    [Fact]
    public void AllProviders_Gemini_HasNonEmptyModels()
    {
        using var provider = new GeminiProvider(NullLogger<GeminiProvider>.Instance);
        provider.ProviderInfo.Models.Should().NotBeEmpty();
    }

    [Fact]
    public void AllProviders_Groq_HasNonEmptyModels()
    {
        using var provider = new GroqProvider(NullLogger<GroqProvider>.Instance);
        provider.ProviderInfo.Models.Should().NotBeEmpty();
    }

    [Fact]
    public void AllProviders_Ollama_HasNonEmptyModels()
    {
        using var provider = new OllamaProvider(NullLogger<OllamaProvider>.Instance);
        provider.ProviderInfo.Models.Should().NotBeEmpty();
    }

    [Fact]
    public void AllProviders_OpenAi_DisposesCorrectly()
    {
        var provider = new OpenAiProvider(NullLogger<OpenAiProvider>.Instance);
        provider.Dispose();
        provider.Dispose(); // Double dispose should be safe
    }

    [Fact]
    public void AllProviders_Claude_DisposesCorrectly()
    {
        var provider = new ClaudeProvider(NullLogger<ClaudeProvider>.Instance);
        provider.Dispose();
        provider.Dispose(); // Double dispose should be safe
    }

    [Fact]
    public void AllProviders_Gemini_DisposesCorrectly()
    {
        var provider = new GeminiProvider(NullLogger<GeminiProvider>.Instance);
        provider.Dispose();
        provider.Dispose(); // Double dispose should be safe
    }

    [Fact]
    public void AllProviders_Groq_DisposesCorrectly()
    {
        var provider = new GroqProvider(NullLogger<GroqProvider>.Instance);
        provider.Dispose();
        provider.Dispose(); // Double dispose should be safe
    }

    [Fact]
    public void AllProviders_Ollama_DisposesCorrectly()
    {
        var provider = new OllamaProvider(NullLogger<OllamaProvider>.Instance);
        provider.Dispose();
        provider.Dispose(); // Double dispose should be safe
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void Provider_WithoutConfiguration_DoesNotSupportVision()
    {
        // Arrange
        using var provider = new OpenAiProvider(NullLogger<OpenAiProvider>.Instance);

        // Assert - Before configuration
        provider.SupportsVision.Should().BeFalse();
    }

    [Fact]
    public void Provider_WithNonVisionModel_DoesNotSupportVision()
    {
        // Arrange
        using var provider = new GroqProvider(NullLogger<GroqProvider>.Instance);
        var config = new AiProviderConfig
        {
            Provider = AiProvider.Groq,
            ApiKey = "test-key",
            VisionModel = "llama-3.3-70b-versatile" // This is NOT a vision model
        };

        // Act
        provider.Configure(config);

        // Assert
        provider.SupportsVision.Should().BeFalse();
    }

    [Fact]
    public void Provider_WithVisionModel_SupportsVision()
    {
        // Arrange
        using var provider = new GroqProvider(NullLogger<GroqProvider>.Instance);
        var config = new AiProviderConfig
        {
            Provider = AiProvider.Groq,
            ApiKey = "test-key",
            VisionModel = "llama-3.2-90b-vision-preview"
        };

        // Act
        provider.Configure(config);

        // Assert
        provider.SupportsVision.Should().BeTrue();
    }

    #endregion

    #region Validation Tests (Without Real API)

    [Fact]
    public async Task Provider_ValidateWithEmptyApiKey_ReturnsFalse()
    {
        // Arrange
        using var provider = new OpenAiProvider(NullLogger<OpenAiProvider>.Instance);
        var config = new AiProviderConfig
        {
            Provider = AiProvider.OpenAI,
            ApiKey = "",
            TextModel = "gpt-4o"
        };
        provider.Configure(config);

        // Act
        var result = await provider.ValidateAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ClaudeProvider_ValidateWithEmptyApiKey_ReturnsFalse()
    {
        // Arrange
        using var provider = new ClaudeProvider(NullLogger<ClaudeProvider>.Instance);
        var config = new AiProviderConfig
        {
            Provider = AiProvider.Claude,
            ApiKey = ""
        };
        provider.Configure(config);

        // Act
        var result = await provider.ValidateAsync();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Analysis Without Configuration Tests

    [Fact]
    public async Task Provider_AnalyzeImageWithoutConfig_ReturnsFailedResult()
    {
        // Arrange
        using var provider = new OpenAiProvider(NullLogger<OpenAiProvider>.Instance);
        var imagePath = _fixture.CreateFile("test.png", "fake png content");

        // Act
        var result = await provider.AnalyzeImageAsync(imagePath);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Provider_AnalyzeDocumentWithoutConfig_ReturnsFailedResult()
    {
        // Arrange
        using var provider = new ClaudeProvider(NullLogger<ClaudeProvider>.Instance);
        var docPath = _fixture.CreateFile("test.txt", "document content");

        // Act
        var result = await provider.AnalyzeDocumentAsync(docPath);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    #endregion
}
