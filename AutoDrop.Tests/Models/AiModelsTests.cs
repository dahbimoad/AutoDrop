using AutoDrop.Models;
using FluentAssertions;

namespace AutoDrop.Tests.Models;

/// <summary>
/// Unit tests for AI-related models.
/// Tests configuration, settings, result classes, and edge cases.
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

    [Theory]
    [InlineData(AiProvider.OpenAI, "OpenAI")]
    [InlineData(AiProvider.Claude, "Claude")]
    [InlineData(AiProvider.Gemini, "Gemini")]
    [InlineData(AiProvider.Groq, "Groq")]
    [InlineData(AiProvider.Ollama, "Ollama")]
    public void AiProvider_ToString_ReturnsCorrectName(AiProvider provider, string expectedName)
    {
        // Assert
        provider.ToString().Should().Be(expectedName);
    }

    [Fact]
    public void AiProvider_InvalidValue_IsNotDefined()
    {
        // Assert
        Enum.IsDefined((AiProvider)99).Should().BeFalse();
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
        config.IsKeySecured.Should().BeFalse();
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
            LastValidated = now,
            IsKeySecured = true
        };

        // Assert
        config.Provider.Should().Be(AiProvider.OpenAI);
        config.ApiKey.Should().Be("sk-test-key");
        config.BaseUrl.Should().Be("https://api.openai.com");
        config.TextModel.Should().Be("gpt-4o");
        config.VisionModel.Should().Be("gpt-4o");
        config.IsValidated.Should().BeTrue();
        config.LastValidated.Should().Be(now);
        config.IsKeySecured.Should().BeTrue();
    }

    [Fact]
    public void AiProviderConfig_CredentialKey_GeneratesCorrectFormat()
    {
        // Arrange
        var config = new AiProviderConfig { Provider = AiProvider.OpenAI };

        // Assert
        config.CredentialKey.Should().Be("AutoDrop_OpenAI_ApiKey");
    }

    [Theory]
    [InlineData(AiProvider.OpenAI, "AutoDrop_OpenAI_ApiKey")]
    [InlineData(AiProvider.Claude, "AutoDrop_Claude_ApiKey")]
    [InlineData(AiProvider.Gemini, "AutoDrop_Gemini_ApiKey")]
    [InlineData(AiProvider.Groq, "AutoDrop_Groq_ApiKey")]
    [InlineData(AiProvider.Ollama, "AutoDrop_Ollama_ApiKey")]
    public void AiProviderConfig_CredentialKey_CorrectForAllProviders(AiProvider provider, string expectedKey)
    {
        // Arrange
        var config = new AiProviderConfig { Provider = provider };

        // Assert
        config.CredentialKey.Should().Be(expectedKey);
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

    [Fact]
    public void AiModelInfo_IsRecord_SupportsEquality()
    {
        // Arrange
        var model1 = new AiModelInfo { Id = "gpt-4o", DisplayName = "GPT-4o" };
        var model2 = new AiModelInfo { Id = "gpt-4o", DisplayName = "GPT-4o" };
        var model3 = new AiModelInfo { Id = "gpt-4o-mini", DisplayName = "GPT-4o Mini" };

        // Assert
        model1.Should().Be(model2);
        model1.Should().NotBe(model3);
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

    [Fact]
    public void AiProviderInfo_IconGlyph_HasDefault()
    {
        // Arrange & Act
        var providerInfo = new AiProviderInfo
        {
            Provider = AiProvider.OpenAI,
            DisplayName = "Test",
            Description = "Test provider",
            Models = []
        };

        // Assert - Default icon glyph
        providerInfo.IconGlyph.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void AiProviderInfo_IsRecord_SupportsEquality()
    {
        // Arrange
        var models = new List<AiModelInfo> { new() { Id = "test", DisplayName = "Test" } };
        var info1 = new AiProviderInfo
        {
            Provider = AiProvider.OpenAI,
            DisplayName = "OpenAI",
            Description = "Test",
            Models = models
        };
        var info2 = new AiProviderInfo
        {
            Provider = AiProvider.OpenAI,
            DisplayName = "OpenAI",
            Description = "Test",
            Models = models
        };

        // Assert
        info1.Should().Be(info2);
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
        settings.DefaultNewFolderBasePath.Should().BeEmpty();
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
        Enum.IsDefined(AiContentType.Archive).Should().BeTrue();
        Enum.IsDefined(AiContentType.Media).Should().BeTrue();
    }

    [Fact]
    public void AiContentType_Unknown_IsDefault()
    {
        // Arrange & Act
        var result = new AiAnalysisResult();

        // Assert
        result.ContentType.Should().Be(AiContentType.Unknown);
    }

    #endregion

    #region AiAnalysisResult Matched Folder Tests

    [Fact]
    public void AiAnalysisResult_HasMatchedFolder_WhenIdIsSet_ReturnsTrue()
    {
        // Arrange
        var result = new AiAnalysisResult
        {
            Success = true,
            MatchedFolderId = "folder-123"
        };

        // Assert
        result.HasMatchedFolder.Should().BeTrue();
    }

    [Fact]
    public void AiAnalysisResult_HasMatchedFolder_WhenIdIsEmpty_ReturnsFalse()
    {
        // Arrange
        var result = new AiAnalysisResult
        {
            Success = true,
            MatchedFolderId = string.Empty
        };

        // Assert
        result.HasMatchedFolder.Should().BeFalse();
    }

    [Fact]
    public void AiAnalysisResult_HasMatchedFolder_WhenIdIsNull_ReturnsFalse()
    {
        // Arrange
        var result = new AiAnalysisResult
        {
            Success = true,
            MatchedFolderId = null
        };

        // Assert
        result.HasMatchedFolder.Should().BeFalse();
    }

    [Fact]
    public void AiAnalysisResult_MatchedFolderProperties_SetCorrectly()
    {
        // Arrange & Act
        var result = new AiAnalysisResult
        {
            Success = true,
            MatchedFolderId = "folder-456",
            MatchedFolderPath = @"C:\Documents\Work",
            MatchedFolderName = "Work Documents"
        };

        // Assert
        result.MatchedFolderId.Should().Be("folder-456");
        result.MatchedFolderPath.Should().Be(@"C:\Documents\Work");
        result.MatchedFolderName.Should().Be("Work Documents");
        result.HasMatchedFolder.Should().BeTrue();
    }

    [Fact]
    public void AiAnalysisResult_SuggestedNewFolderPath_SetCorrectly()
    {
        // Arrange & Act
        var result = new AiAnalysisResult
        {
            Success = true,
            Category = "Travel",
            SuggestedNewFolderPath = "Pictures/Travel/2024"
        };

        // Assert
        result.SuggestedNewFolderPath.Should().Be("Pictures/Travel/2024");
        result.HasMatchedFolder.Should().BeFalse();
    }

    #endregion

    #region AiAnalysisResult Successful Factory Tests

    [Fact]
    public void AiAnalysisResult_Successful_CreatesSuccessfulResult()
    {
        // Act
        var result = AiAnalysisResult.Successful("Receipts", 0.95, AiContentType.Document);

        // Assert
        result.Success.Should().BeTrue();
        result.Category.Should().Be("Receipts");
        result.Confidence.Should().Be(0.95);
        result.ContentType.Should().Be(AiContentType.Document);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void AiAnalysisResult_Successful_ForImage_SetsContentType()
    {
        // Act
        var result = AiAnalysisResult.Successful("Landscapes", 0.85, AiContentType.Image);

        // Assert
        result.ContentType.Should().Be(AiContentType.Image);
    }

    #endregion

    #region AiSettings ResolvedNewFolderBasePath Tests

    [Fact]
    public void AiSettings_ResolvedNewFolderBasePath_WhenEmpty_ReturnsDocumentsFolder()
    {
        // Arrange
        var settings = new AiSettings
        {
            DefaultNewFolderBasePath = string.Empty
        };

        // Act
        var resolved = settings.ResolvedNewFolderBasePath;

        // Assert
        resolved.Should().Be(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
    }

    [Fact]
    public void AiSettings_ResolvedNewFolderBasePath_WhenInvalidPath_ReturnsDocumentsFolder()
    {
        // Arrange
        var settings = new AiSettings
        {
            DefaultNewFolderBasePath = @"Z:\NonExistent\Path\That\Does\Not\Exist"
        };

        // Act
        var resolved = settings.ResolvedNewFolderBasePath;

        // Assert
        resolved.Should().Be(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
    }

    [Fact]
    public void AiSettings_ResolvedNewFolderBasePath_WhenValidPath_ReturnsConfiguredPath()
    {
        // Arrange
        var tempPath = Path.GetTempPath();
        var settings = new AiSettings
        {
            DefaultNewFolderBasePath = tempPath
        };

        // Act
        var resolved = settings.ResolvedNewFolderBasePath;

        // Assert
        resolved.Should().Be(tempPath);
    }

    #endregion

    #region AiSettings ConfidenceThreshold Tests

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.7)]
    [InlineData(1.0)]
    public void AiSettings_ConfidenceThreshold_AcceptsValidValues(double threshold)
    {
        // Arrange & Act
        var settings = new AiSettings { ConfidenceThreshold = threshold };

        // Assert
        settings.ConfidenceThreshold.Should().Be(threshold);
    }

    #endregion

    #region AiSettings MaxFileSizeMb Tests

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public void AiSettings_MaxFileSizeMb_AcceptsValidValues(int maxSize)
    {
        // Arrange & Act
        var settings = new AiSettings { MaxFileSizeMb = maxSize };

        // Assert
        settings.MaxFileSizeMb.Should().Be(maxSize);
    }

    #endregion

    #region AiSettings ProviderConfigs Tests

    [Fact]
    public void AiSettings_ProviderConfigs_CanAddMultipleProviders()
    {
        // Arrange & Act
        var settings = new AiSettings
        {
            ProviderConfigs =
            [
                new AiProviderConfig { Provider = AiProvider.OpenAI, ApiKey = "key1" },
                new AiProviderConfig { Provider = AiProvider.Claude, ApiKey = "key2" },
                new AiProviderConfig { Provider = AiProvider.Groq, ApiKey = "key3" }
            ]
        };

        // Assert
        settings.ProviderConfigs.Should().HaveCount(3);
        settings.ProviderConfigs.Should().Contain(c => c.Provider == AiProvider.OpenAI);
        settings.ProviderConfigs.Should().Contain(c => c.Provider == AiProvider.Claude);
        settings.ProviderConfigs.Should().Contain(c => c.Provider == AiProvider.Groq);
    }

    [Fact]
    public void AiSettings_IsFullyConfigured_WithMismatchedActiveProvider_ReturnsFalse()
    {
        // Arrange - Active provider is OpenAI but only Groq is configured
        var settings = new AiSettings
        {
            Enabled = true,
            DisclaimerAccepted = true,
            ActiveProvider = AiProvider.OpenAI,
            ProviderConfigs = [
                new AiProviderConfig
                {
                    Provider = AiProvider.Groq,
                    ApiKey = "test-key"
                }
            ]
        };

        // Assert
        settings.IsFullyConfigured.Should().BeFalse();
    }

    #endregion

    #region JSON Serialization Attributes Tests

    [Fact]
    public void AiAnalysisResult_JsonIgnore_ExcludesMatchedFolderPath()
    {
        // This tests that JsonIgnore attributes are properly applied
        var result = new AiAnalysisResult
        {
            Success = true,
            MatchedFolderId = "id-123",
            MatchedFolderPath = @"C:\Test",
            MatchedFolderName = "Test Folder"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(result);

        // MatchedFolderPath and MatchedFolderName should be excluded
        json.Should().NotContain("MatchedFolderPath");
        json.Should().NotContain("MatchedFolderName");
        json.Should().NotContain("HasMatchedFolder");
        
        // But matchedFolderId should be included
        json.Should().Contain("matchedFolderId");
    }

    [Fact]
    public void AiProviderConfig_JsonPropertyName_UsesCamelCase()
    {
        // Arrange
        var config = new AiProviderConfig
        {
            Provider = AiProvider.OpenAI,
            ApiKey = "test-key",
            IsKeySecured = true
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(config);

        // Assert - Should use camelCase
        json.Should().Contain("\"provider\"");
        json.Should().Contain("\"apiKey\"");
        json.Should().Contain("\"isKeySecured\"");
    }

    [Fact]
    public void AiSettings_JsonPropertyName_UsesCamelCase()
    {
        // Arrange
        var settings = new AiSettings
        {
            Enabled = true,
            DisclaimerAccepted = true,
            EnableVisionAnalysis = false
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(settings);

        // Assert
        json.Should().Contain("\"enabled\"");
        json.Should().Contain("\"disclaimerAccepted\"");
        json.Should().Contain("\"enableVisionAnalysis\"");
    }

    #endregion
}
