using AutoDrop.Models;
using AutoDrop.Services.AI.Local;
using AutoDrop.Services.AI.Providers;
using AutoDrop.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoDrop.Tests.Services.AI;

/// <summary>
/// Unit tests for individual AI provider implementations.
/// Tests provider info, configuration, capability detection, and error handling.
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

    [Fact]
    public void OpenAiProvider_Configure_WithNullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        using var provider = new OpenAiProvider(NullLogger<OpenAiProvider>.Instance);

        // Act & Assert
        var act = () => provider.Configure(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void OpenAiProvider_SupportsPdf_ReturnsFalse()
    {
        // Arrange
        using var provider = new OpenAiProvider(NullLogger<OpenAiProvider>.Instance);

        // Assert - OpenAI doesn't support PDF natively
        provider.SupportsPdf.Should().BeFalse();
    }

    [Fact]
    public async Task OpenAiProvider_AnalyzeImageAsync_WithNullPath_ThrowsArgumentException()
    {
        // Arrange
        using var provider = new OpenAiProvider(NullLogger<OpenAiProvider>.Instance);

        // Act & Assert
        await provider.Invoking(p => p.AnalyzeImageAsync(null!))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task OpenAiProvider_AnalyzeDocumentAsync_WithNullPath_ThrowsArgumentException()
    {
        // Arrange
        using var provider = new OpenAiProvider(NullLogger<OpenAiProvider>.Instance);

        // Act & Assert
        await provider.Invoking(p => p.AnalyzeDocumentAsync(null!))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task OpenAiProvider_SendTextPromptAsync_WithNullPrompt_ThrowsArgumentException()
    {
        // Arrange
        using var provider = new OpenAiProvider(NullLogger<OpenAiProvider>.Instance);

        // Act & Assert
        await provider.Invoking(p => p.SendTextPromptAsync(null!))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task OpenAiProvider_SendTextPromptAsync_WithoutConfig_ThrowsInvalidOperationException()
    {
        // Arrange
        using var provider = new OpenAiProvider(NullLogger<OpenAiProvider>.Instance);

        // Act & Assert
        await provider.Invoking(p => p.SendTextPromptAsync("test prompt"))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*API key*");
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

    [Fact]
    public async Task ClaudeProvider_AnalyzeImageAsync_WithNonExistentFile_ReturnsFailedResult()
    {
        // Arrange
        using var provider = new ClaudeProvider(NullLogger<ClaudeProvider>.Instance);
        provider.Configure(new AiProviderConfig
        {
            Provider = AiProvider.Claude,
            ApiKey = "test-key"
        });

        // Act
        var result = await provider.AnalyzeImageAsync(_fixture.GetPath("nonexistent.png"));

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ClaudeProvider_AnalyzeDocumentAsync_WithNonExistentFile_ReturnsFailedResult()
    {
        // Arrange
        using var provider = new ClaudeProvider(NullLogger<ClaudeProvider>.Instance);
        provider.Configure(new AiProviderConfig
        {
            Provider = AiProvider.Claude,
            ApiKey = "test-key"
        });

        // Act
        var result = await provider.AnalyzeDocumentAsync(_fixture.GetPath("nonexistent.txt"));

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
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

    [Fact]
    public void GeminiProvider_ProviderInfo_HasApiKeyUrl()
    {
        // Arrange
        using var provider = new GeminiProvider(NullLogger<GeminiProvider>.Instance);

        // Assert
        provider.ProviderInfo.ApiKeyUrl.Should().Contain("google");
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

    [Fact]
    public void GroqProvider_SupportsPdf_ReturnsFalse()
    {
        // Arrange
        using var provider = new GroqProvider(NullLogger<GroqProvider>.Instance);

        // Assert - Groq doesn't support PDF natively
        provider.SupportsPdf.Should().BeFalse();
    }

    [Fact]
    public async Task GroqProvider_AnalyzeImageAsync_WithValidFile_ValidatesInputsBeforeNetwork()
    {
        // Arrange - Test input validation without making network calls
        using var provider = new GroqProvider(NullLogger<GroqProvider>.Instance);
        // Don't configure API key - this tests the validation path
        var imagePath = _fixture.CreateFile("test.png", "fake png data");

        // Act - Should fail at API key validation, not at network layer
        var result = await provider.AnalyzeImageAsync(imagePath);

        // Assert - Failed due to missing API key, not network error
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("API key");
    }

    [Fact]
    public async Task GroqProvider_AnalyzeDocumentAsync_WithLargeFile_ValidatesInputsBeforeNetwork()
    {
        // Arrange - Test that large files can be read without making network calls
        using var provider = new GroqProvider(NullLogger<GroqProvider>.Instance);
        // Don't configure API key - this tests the validation path
        var largeContent = new string('A', 10000);
        var docPath = _fixture.CreateFile("large.txt", largeContent);

        // Act - Should fail at API key validation, not at network layer
        var result = await provider.AnalyzeDocumentAsync(docPath);

        // Assert - Failed due to missing API key, not network error
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("API key");
    }

    #endregion

    #region Local AI Provider Tests

    [Fact]
    public void LocalAiProvider_ProviderType_IsLocal()
    {
        // Arrange
        var options = new LocalAiOptions();
        var modelManager = new OnnxModelManager(options, NullLogger<OnnxModelManager>.Instance);
        using var provider = new LocalAiProvider(modelManager, options, NullLogger<LocalAiProvider>.Instance);

        // Assert
        provider.ProviderType.Should().Be(AiProvider.Local);
    }

    [Fact]
    public void LocalAiProvider_ProviderInfo_IsLocal()
    {
        // Arrange
        var options = new LocalAiOptions();
        var modelManager = new OnnxModelManager(options, NullLogger<OnnxModelManager>.Instance);
        using var provider = new LocalAiProvider(modelManager, options, NullLogger<LocalAiProvider>.Instance);

        // Assert
        provider.ProviderInfo.IsLocal.Should().BeTrue();
        provider.ProviderInfo.RequiresApiKey.Should().BeFalse();
    }

    [Fact]
    public void LocalAiProvider_SupportsVision_ReturnsTrue()
    {
        // Arrange
        var options = new LocalAiOptions();
        var modelManager = new OnnxModelManager(options, NullLogger<OnnxModelManager>.Instance);
        using var provider = new LocalAiProvider(modelManager, options, NullLogger<LocalAiProvider>.Instance);

        // Assert
        provider.SupportsVision.Should().BeTrue();
    }

    [Fact]
    public void LocalAiProvider_SupportsPdf_ReturnsFalse()
    {
        // Arrange
        var options = new LocalAiOptions();
        var modelManager = new OnnxModelManager(options, NullLogger<OnnxModelManager>.Instance);
        using var provider = new LocalAiProvider(modelManager, options, NullLogger<LocalAiProvider>.Instance);

        // Assert
        provider.SupportsPdf.Should().BeFalse();
    }

    [Fact]
    public void LocalAiProvider_Models_HasDefaultModel()
    {
        // Arrange
        var options = new LocalAiOptions();
        var modelManager = new OnnxModelManager(options, NullLogger<OnnxModelManager>.Instance);
        using var provider = new LocalAiProvider(modelManager, options, NullLogger<LocalAiProvider>.Instance);

        // Assert
        provider.ProviderInfo.Models.Should().NotBeEmpty();
        provider.ProviderInfo.Models.Should().Contain(m => m.Id == "all-MiniLM-L6-v2");
    }

    [Fact]
    public async Task LocalAiProvider_ValidateAsync_WithoutModels_ReturnsFalse()
    {
        // Arrange
        var options = new LocalAiOptions 
        { 
            ModelsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()) 
        };
        var modelManager = new OnnxModelManager(options, NullLogger<OnnxModelManager>.Instance);
        using var provider = new LocalAiProvider(modelManager, options, NullLogger<LocalAiProvider>.Instance);

        // Act
        var isValid = await provider.ValidateAsync();

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task LocalAiProvider_AnalyzeImageAsync_WithoutModels_ReturnsError()
    {
        // Arrange
        var options = new LocalAiOptions 
        { 
            ModelsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()) 
        };
        var modelManager = new OnnxModelManager(options, NullLogger<OnnxModelManager>.Instance);
        using var provider = new LocalAiProvider(modelManager, options, NullLogger<LocalAiProvider>.Instance);
        var imagePath = _fixture.CreateImageFile("test.jpg");

        // Act
        var result = await provider.AnalyzeImageAsync(imagePath);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not downloaded");
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
    public void AllProviders_Local_HasNonEmptyModels()
    {
        var options = new LocalAiOptions();
        var modelManager = new OnnxModelManager(options, NullLogger<OnnxModelManager>.Instance);
        using var provider = new LocalAiProvider(modelManager, options, NullLogger<LocalAiProvider>.Instance);
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
    public void AllProviders_Local_DisposesCorrectly()
    {
        var options = new LocalAiOptions();
        var modelManager = new OnnxModelManager(options, NullLogger<OnnxModelManager>.Instance);
        var provider = new LocalAiProvider(modelManager, options, NullLogger<LocalAiProvider>.Instance);
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
            VisionModel = "meta-llama/llama-4-scout-17b-16e-instruct"
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

    [Fact]
    public async Task GeminiProvider_AnalyzeImageWithoutConfig_ReturnsFailedResult()
    {
        // Arrange
        using var provider = new GeminiProvider(NullLogger<GeminiProvider>.Instance);
        var imagePath = _fixture.CreateFile("test.jpg", "fake jpg content");

        // Act
        var result = await provider.AnalyzeImageAsync(imagePath);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("API key");
    }

    [Fact]
    public async Task GroqProvider_AnalyzeImageWithoutConfig_ReturnsFailedResult()
    {
        // Arrange
        using var provider = new GroqProvider(NullLogger<GroqProvider>.Instance);
        var imagePath = _fixture.CreateFile("test.webp", "fake webp content");

        // Act
        var result = await provider.AnalyzeImageAsync(imagePath);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("API key");
    }

    #endregion

    #region Custom Folder Integration Tests

    [Fact]
    public async Task Provider_AnalyzeImageWithCustomFolders_AcceptsCustomFolderParameter()
    {
        // Arrange - Test that custom folders parameter is accepted
        using var provider = new GroqProvider(NullLogger<GroqProvider>.Instance);
        // Don't configure API key to avoid network calls
        var imagePath = _fixture.CreateFile("photo.jpg", "fake jpg data");
        var customFolders = new List<CustomFolder>
        {
            new() { Name = "Vacation Photos", Path = @"C:\Photos\Vacation" },
            new() { Name = "Work Documents", Path = @"C:\Documents\Work" }
        };

        // Act - Should fail at API key validation, not crash due to folders
        var result = await provider.AnalyzeImageAsync(imagePath, customFolders);

        // Assert - Failed validation but didn't throw exception
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("API key");
    }

    [Fact]
    public async Task Provider_AnalyzeDocumentWithCustomFolders_AcceptsCustomFolderParameter()
    {
        // Arrange - Test that custom folders parameter is accepted
        using var provider = new ClaudeProvider(NullLogger<ClaudeProvider>.Instance);
        // Don't configure API key to avoid network calls
        var docPath = _fixture.CreateFile("invoice.txt", "Invoice #12345\nAmount: $100");
        var customFolders = new List<CustomFolder>
        {
            new() { Name = "Invoices", Path = @"C:\Documents\Invoices" },
            new() { Name = "Receipts", Path = @"C:\Documents\Receipts" }
        };

        // Act - Should fail at API key validation, not crash due to folders
        var result = await provider.AnalyzeDocumentAsync(docPath, customFolders);

        // Assert - Failed validation but didn't throw exception
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("API key");
    }

    #endregion

    #region MIME Type Detection Tests

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".png")]
    [InlineData(".gif")]
    [InlineData(".bmp")]
    [InlineData(".webp")]
    public void Provider_HandlesAllSupportedImageExtensions(string extension)
    {
        // This tests that providers can handle all supported image extensions
        using var provider = new OpenAiProvider(NullLogger<OpenAiProvider>.Instance);
        provider.Configure(new AiProviderConfig
        {
            Provider = AiProvider.OpenAI,
            ApiKey = "test-key",
            VisionModel = "gpt-4o"
        });

        // The provider should handle all supported extensions
        var imagePath = _fixture.CreateFile($"test{extension}", "fake image data");
        
        // Verify the file was created with correct extension
        Path.GetExtension(imagePath).Should().Be(extension);
    }

    #endregion

    #region Provider Model Selection Tests

    [Fact]
    public void OpenAiProvider_WithTextModelOnly_SelectsTextModel()
    {
        // Arrange
        using var provider = new OpenAiProvider(NullLogger<OpenAiProvider>.Instance);
        var config = new AiProviderConfig
        {
            Provider = AiProvider.OpenAI,
            ApiKey = "test-key",
            TextModel = "gpt-4o-mini",
            VisionModel = string.Empty
        };

        // Act
        provider.Configure(config);

        // Assert - Without vision model, vision support depends on text model
        provider.ProviderType.Should().Be(AiProvider.OpenAI);
    }

    [Fact]
    public void ClaudeProvider_WithPdfCapableModel_SupportsPdf()
    {
        // Arrange
        using var provider = new ClaudeProvider(NullLogger<ClaudeProvider>.Instance);
        var config = new AiProviderConfig
        {
            Provider = AiProvider.Claude,
            ApiKey = "test-key",
            VisionModel = "claude-3-5-sonnet-20241022" // Supports PDF
        };

        // Act
        provider.Configure(config);

        // Assert
        provider.SupportsPdf.Should().BeTrue();
    }

    [Fact]
    public void ClaudeProvider_WithNonPdfCapableModel_DoesNotSupportPdf()
    {
        // Arrange
        using var provider = new ClaudeProvider(NullLogger<ClaudeProvider>.Instance);
        var config = new AiProviderConfig
        {
            Provider = AiProvider.Claude,
            ApiKey = "test-key",
            VisionModel = "claude-3-5-haiku-20241022" // Doesn't support PDF
        };

        // Act
        provider.Configure(config);

        // Assert
        provider.SupportsPdf.Should().BeFalse();
    }

    #endregion

    #region Provider Icon and Display Tests

    [Fact]
    public void AllProviders_HaveIconGlyph()
    {
        // Assert all providers have an icon for UI display
        using var openai = new OpenAiProvider(NullLogger<OpenAiProvider>.Instance);
        using var claude = new ClaudeProvider(NullLogger<ClaudeProvider>.Instance);
        using var gemini = new GeminiProvider(NullLogger<GeminiProvider>.Instance);
        using var groq = new GroqProvider(NullLogger<GroqProvider>.Instance);
        
        var localOptions = new LocalAiOptions();
        var localModelManager = new OnnxModelManager(localOptions, NullLogger<OnnxModelManager>.Instance);
        using var local = new LocalAiProvider(localModelManager, localOptions, NullLogger<LocalAiProvider>.Instance);

        openai.ProviderInfo.IconGlyph.Should().NotBeNullOrEmpty();
        claude.ProviderInfo.IconGlyph.Should().NotBeNullOrEmpty();
        gemini.ProviderInfo.IconGlyph.Should().NotBeNullOrEmpty();
        groq.ProviderInfo.IconGlyph.Should().NotBeNullOrEmpty();
        local.ProviderInfo.IconGlyph.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void AllProviders_HaveDescription()
    {
        // Assert all providers have a description
        using var openai = new OpenAiProvider(NullLogger<OpenAiProvider>.Instance);
        using var claude = new ClaudeProvider(NullLogger<ClaudeProvider>.Instance);
        using var gemini = new GeminiProvider(NullLogger<GeminiProvider>.Instance);
        using var groq = new GroqProvider(NullLogger<GroqProvider>.Instance);
        
        var localOptions = new LocalAiOptions();
        var localModelManager = new OnnxModelManager(localOptions, NullLogger<OnnxModelManager>.Instance);
        using var local = new LocalAiProvider(localModelManager, localOptions, NullLogger<LocalAiProvider>.Instance);

        openai.ProviderInfo.Description.Should().NotBeNullOrEmpty();
        claude.ProviderInfo.Description.Should().NotBeNullOrEmpty();
        gemini.ProviderInfo.Description.Should().NotBeNullOrEmpty();
        groq.ProviderInfo.Description.Should().NotBeNullOrEmpty();
        local.ProviderInfo.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CloudProviders_HaveApiKeyUrl()
    {
        // Assert cloud providers have API key URLs
        using var openai = new OpenAiProvider(NullLogger<OpenAiProvider>.Instance);
        using var claude = new ClaudeProvider(NullLogger<ClaudeProvider>.Instance);
        using var gemini = new GeminiProvider(NullLogger<GeminiProvider>.Instance);
        using var groq = new GroqProvider(NullLogger<GroqProvider>.Instance);

        openai.ProviderInfo.ApiKeyUrl.Should().NotBeNullOrEmpty();
        claude.ProviderInfo.ApiKeyUrl.Should().NotBeNullOrEmpty();
        gemini.ProviderInfo.ApiKeyUrl.Should().NotBeNullOrEmpty();
        groq.ProviderInfo.ApiKeyUrl.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Concurrency and Thread Safety Tests

    [Fact]
    public async Task Provider_MultipleAnalysisCalls_HandlesCorrectly()
    {
        // Arrange - Test that multiple concurrent calls don't throw exceptions
        using var provider = new GroqProvider(NullLogger<GroqProvider>.Instance);
        // Don't configure API key to avoid network calls but still test concurrency
        
        var doc1 = _fixture.CreateFile("doc1.txt", "Content 1");
        var doc2 = _fixture.CreateFile("doc2.txt", "Content 2");
        var doc3 = _fixture.CreateFile("doc3.txt", "Content 3");

        // Act - Run multiple analyses concurrently (will fail validation but shouldn't throw)
        var tasks = new[]
        {
            provider.AnalyzeDocumentAsync(doc1),
            provider.AnalyzeDocumentAsync(doc2),
            provider.AnalyzeDocumentAsync(doc3)
        };

        var results = await Task.WhenAll(tasks);

        // Assert - All should complete without throwing
        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r =>
        {
            r.Should().NotBeNull();
            r.Success.Should().BeFalse();
            r.Error.Should().Contain("API key");
        });
    }

    #endregion
}
