using AutoDrop.Models;
using FluentAssertions;

namespace AutoDrop.Tests.Models;

/// <summary>
/// Unit tests for AI-related models.
/// Tests configuration, settings, and result classes.
/// </summary>
public sealed class AiModelsTests
{
    #region AiProvider Enum Tests

    [Fact]
    public void AiProvider_HasAllExpectedValues()
    {
        // Assert
        Enum.GetValues<AiProvider>().Should().HaveCount(5);
        Enum.IsDefined(AiProvider.OpenAI).Should().BeTrue();
        Enum.IsDefined(AiProvider.Claude).Should().BeTrue();
        Enum.IsDefined(AiProvider.Gemini).Should().BeTrue();
        Enum.IsDefined(AiProvider.Groq).Should().BeTrue();
        Enum.IsDefined(AiProvider.Ollama).Should().BeTrue();
    }

    #endregion

    #region AiProviderConfig Tests

    [Fact]
    public void AiProviderConfig_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new AiProviderConfig();

        // Assert
        config.ApiKey.Should().BeEmpty();
        config.BaseUrl.Should().BeNull();
        config.TextModel.Should().BeEmpty();
        config.VisionModel.Should().BeEmpty();
        config.IsValidated.Should().BeFalse();
        config.LastValidated.Should().BeNull();
    }

    [Fact]
    public void AiProviderConfig_SetProperties_PersistsValues()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var config = new AiProviderConfig
        {
            Provider = AiProvider.OpenAI,
            ApiKey = "sk-test-key",
            BaseUrl = "https://api.openai.com",
            TextModel = "gpt-4o",
            VisionModel = "gpt-4o",
            IsValidated = true,
            LastValidated = now
        };

        // Assert
        config.Provider.Should().Be(AiProvider.OpenAI);
        config.ApiKey.Should().Be("sk-test-key");
        config.BaseUrl.Should().Be("https://api.openai.com");
        config.TextModel.Should().Be("gpt-4o");
        config.VisionModel.Should().Be("gpt-4o");
        config.IsValidated.Should().BeTrue();
        config.LastValidated.Should().Be(now);
    }

    #endregion

    #region AiModelInfo Tests

    [Fact]
    public void AiModelInfo_RequiredProperties_MustBeSet()
    {
        // Arrange & Act
        var model = new AiModelInfo
        {
            Id = "gpt-4o",
            DisplayName = "GPT-4o"
        };

        // Assert
        model.Id.Should().Be("gpt-4o");
        model.DisplayName.Should().Be("GPT-4o");
    }

    [Fact]
    public void AiModelInfo_OptionalProperties_HaveDefaults()
    {
        // Arrange & Act
        var model = new AiModelInfo
        {
            Id = "test-model",
            DisplayName = "Test Model"
        };

        // Assert
        model.SupportsVision.Should().BeFalse();
        model.SupportsPdf.Should().BeFalse();
        model.MaxTokens.Should().Be(0);
        model.Description.Should().BeNull();
    }

    [Fact]
    public void AiModelInfo_WithVisionSupport_ReturnsTrue()
    {
        // Arrange & Act
        var model = new AiModelInfo
        {
            Id = "gpt-4-vision",
            DisplayName = "GPT-4 Vision",
            SupportsVision = true,
            SupportsPdf = true,
            MaxTokens = 128000,
            Description = "Vision-capable model"
        };

        // Assert
        model.SupportsVision.Should().BeTrue();
        model.SupportsPdf.Should().BeTrue();
        model.MaxTokens.Should().Be(128000);
        model.Description.Should().Be("Vision-capable model");
    }

    #endregion

    #region AiProviderInfo Tests

    [Fact]
    public void AiProviderInfo_CloudProvider_HasCorrectDefaults()
    {
        // Arrange & Act
        var providerInfo = new AiProviderInfo
        {
            Provider = AiProvider.OpenAI,
            DisplayName = "OpenAI",
            Description = "GPT-4 and GPT-4o models",
            ApiKeyUrl = "https://platform.openai.com/api-keys",
            Models = [
                new AiModelInfo { Id = "gpt-4o", DisplayName = "GPT-4o" }
            ]
        };

        // Assert
        providerInfo.IsLocal.Should().BeFalse();
        providerInfo.RequiresApiKey.Should().BeTrue();
    }

    [Fact]
    public void AiProviderInfo_LocalProvider_HasCorrectSettings()
    {
        // Arrange & Act
        var providerInfo = new AiProviderInfo
        {
            Provider = AiProvider.Ollama,
            DisplayName = "Ollama (Local)",
            Description = "Local AI for privacy",
            IsLocal = true,
            RequiresApiKey = false,
            DefaultBaseUrl = "http://localhost:11434",
            Models = [
                new AiModelInfo { Id = "llava", DisplayName = "LLaVA" }
            ]
        };

        // Assert
        providerInfo.IsLocal.Should().BeTrue();
        providerInfo.RequiresApiKey.Should().BeFalse();
        providerInfo.DefaultBaseUrl.Should().Be("http://localhost:11434");
    }

    #endregion

    #region AiSettings Tests

    [Fact]
    public void AiSettings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var settings = new AiSettings();

        // Assert
        settings.Enabled.Should().BeFalse();
        settings.DisclaimerAccepted.Should().BeFalse();
        settings.ActiveProvider.Should().Be(AiProvider.Groq);
        settings.ConfidenceThreshold.Should().Be(0.7);
        settings.EnableSmartRename.Should().BeTrue();
        settings.EnableVisionAnalysis.Should().BeTrue();
        settings.EnableDocumentAnalysis.Should().BeTrue();
        settings.MaxFileSizeMb.Should().Be(10);
        settings.ProviderConfigs.Should().BeEmpty();
    }

    [Fact]
    public void AiSettings_IsFullyConfigured_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        var settings = new AiSettings
        {
            Enabled = false,
            DisclaimerAccepted = true
        };

        // Assert
        settings.IsFullyConfigured.Should().BeFalse();
    }

    [Fact]
    public void AiSettings_IsFullyConfigured_WhenDisclaimerNotAccepted_ReturnsFalse()
    {
        // Arrange
        var settings = new AiSettings
        {
            Enabled = true,
            DisclaimerAccepted = false
        };

        // Assert
        settings.IsFullyConfigured.Should().BeFalse();
    }

    [Fact]
    public void AiSettings_IsFullyConfigured_WhenNoConfig_ReturnsFalse()
    {
        // Arrange
        var settings = new AiSettings
        {
            Enabled = true,
            DisclaimerAccepted = true,
            ActiveProvider = AiProvider.OpenAI,
            ProviderConfigs = []
        };

        // Assert
        settings.IsFullyConfigured.Should().BeFalse();
    }

    [Fact]
    public void AiSettings_IsFullyConfigured_WithCloudProviderAndApiKey_ReturnsTrue()
    {
        // Arrange
        var settings = new AiSettings
        {
            Enabled = true,
            DisclaimerAccepted = true,
            ActiveProvider = AiProvider.OpenAI,
            ProviderConfigs = [
                new AiProviderConfig
                {
                    Provider = AiProvider.OpenAI,
                    ApiKey = "sk-test-key"
                }
            ]
        };

        // Assert
        settings.IsFullyConfigured.Should().BeTrue();
    }

    [Fact]
    public void AiSettings_IsFullyConfigured_WithCloudProviderNoApiKey_ReturnsFalse()
    {
        // Arrange
        var settings = new AiSettings
        {
            Enabled = true,
            DisclaimerAccepted = true,
            ActiveProvider = AiProvider.OpenAI,
            ProviderConfigs = [
                new AiProviderConfig
                {
                    Provider = AiProvider.OpenAI,
                    ApiKey = ""
                }
            ]
        };

        // Assert
        settings.IsFullyConfigured.Should().BeFalse();
    }

    [Fact]
    public void AiSettings_IsFullyConfigured_WithOllamaNoApiKey_ReturnsTrue()
    {
        // Arrange
        var settings = new AiSettings
        {
            Enabled = true,
            DisclaimerAccepted = true,
            ActiveProvider = AiProvider.Ollama,
            ProviderConfigs = [
                new AiProviderConfig
                {
                    Provider = AiProvider.Ollama,
                    ApiKey = "" // Ollama doesn't need API key
                }
            ]
        };

        // Assert
        settings.IsFullyConfigured.Should().BeTrue();
    }

    #endregion

    #region AiAnalysisResult Tests

    [Fact]
    public void AiAnalysisResult_Failed_CreatesFailedResult()
    {
        // Act
        var result = AiAnalysisResult.Failed("Test error message");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Test error message");
        result.Category.Should().BeEmpty();
        result.Confidence.Should().Be(0);
    }

    [Fact]
    public void AiAnalysisResult_Success_HasCorrectProperties()
    {
        // Arrange
        var result = new AiAnalysisResult
        {
            Success = true,
            Category = "Receipts",
            Subcategory = "Shopping",
            SuggestedName = "amazon_receipt_2024",
            Description = "Amazon purchase receipt",
            Confidence = 0.95,
            ContentType = AiContentType.Document
        };

        // Assert
        result.Success.Should().BeTrue();
        result.Category.Should().Be("Receipts");
        result.Subcategory.Should().Be("Shopping");
        result.SuggestedName.Should().Be("amazon_receipt_2024");
        result.Description.Should().Be("Amazon purchase receipt");
        result.Confidence.Should().Be(0.95);
        result.ContentType.Should().Be(AiContentType.Document);
        result.Error.Should().BeNull();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void AiAnalysisResult_ConfidenceRange_AcceptsValidValues(double confidence)
    {
        // Arrange & Act
        var result = new AiAnalysisResult { Confidence = confidence };

        // Assert
        result.Confidence.Should().Be(confidence);
    }

    #endregion

    #region AiContentType Enum Tests

    [Fact]
    public void AiContentType_HasExpectedValues()
    {
        // Assert
        Enum.IsDefined(AiContentType.Unknown).Should().BeTrue();
        Enum.IsDefined(AiContentType.Image).Should().BeTrue();
        Enum.IsDefined(AiContentType.Document).Should().BeTrue();
        Enum.IsDefined(AiContentType.Code).Should().BeTrue();
    }

    #endregion
}
