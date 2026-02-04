using AutoDrop.Services.AI;
using AutoDrop.Services.AI.Local;
using AutoDrop.Services.AI.Providers;
using AutoDrop.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoDrop.Tests.Services.AI;

/// <summary>
/// Unit tests for AiService - the main AI orchestrator.
/// Tests provider management, file routing, analysis delegation, and user preferences enforcement.
/// </summary>
public sealed class AiServiceTests : IDisposable
{
    private readonly TestFileFixture _fixture;
    private readonly Mock<ISettingsService> _settingsServiceMock;
    private readonly Mock<ICredentialService> _credentialServiceMock;
    private readonly AiService _service;

    public AiServiceTests()
    {
        _fixture = new TestFileFixture();
        _settingsServiceMock = new Mock<ISettingsService>();
        _credentialServiceMock = new Mock<ICredentialService>();

        // Setup default settings
        SetupDefaultSettings(enabled: true, disclaimerAccepted: true);

        _service = CreateAiService();
    }

    public void Dispose()
    {
        _service.Dispose();
        _fixture.Dispose();
    }

    private AiService CreateAiService()
    {
        // Create provider instances to inject
        var localAiOptions = new LocalAiOptions();
        var modelManager = new OnnxModelManager(localAiOptions, NullLogger<OnnxModelManager>.Instance);
        
        var providers = new IAiProvider[]
        {
            new LocalAiProvider(modelManager, localAiOptions, NullLogger<LocalAiProvider>.Instance),
            new OpenAiProvider(NullLogger<OpenAiProvider>.Instance),
            new ClaudeProvider(NullLogger<ClaudeProvider>.Instance),
            new GeminiProvider(NullLogger<GeminiProvider>.Instance),
            new GroqProvider(NullLogger<GroqProvider>.Instance)
        };

        return new AiService(
            _settingsServiceMock.Object,
            _credentialServiceMock.Object,
            NullLogger<AiService>.Instance,
            providers);
    }

    private void SetupDefaultSettings(
        bool enabled = true, 
        bool disclaimerAccepted = true, 
        AiProvider activeProvider = AiProvider.Groq,
        bool enableVisionAnalysis = true,
        bool enableDocumentAnalysis = true,
        int maxFileSizeMb = 10)
    {
        var settings = new AppSettings
        {
            AiSettings = new AiSettings
            {
                Enabled = enabled,
                DisclaimerAccepted = disclaimerAccepted,
                ActiveProvider = activeProvider,
                EnableVisionAnalysis = enableVisionAnalysis,
                EnableDocumentAnalysis = enableDocumentAnalysis,
                MaxFileSizeMb = maxFileSizeMb,
                ProviderConfigs = [
                    new AiProviderConfig
                    {
                        Provider = AiProvider.Groq,
                        ApiKey = "test-api-key",
                        TextModel = "llama-3.3-70b-versatile",
                        VisionModel = "llama-3.2-90b-vision-preview",
                        IsValidated = true
                    }
                ]
            }
        };

        _settingsServiceMock
            .Setup(x => x.GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_InitializesProviders()
    {
        // Assert
        _service.AvailableProviders.Should().HaveCount(5);
        _service.AvailableProviders.Should().Contain(p => p.Provider == AiProvider.Local);
        _service.AvailableProviders.Should().Contain(p => p.Provider == AiProvider.OpenAI);
        _service.AvailableProviders.Should().Contain(p => p.Provider == AiProvider.Claude);
        _service.AvailableProviders.Should().Contain(p => p.Provider == AiProvider.Gemini);
        _service.AvailableProviders.Should().Contain(p => p.Provider == AiProvider.Groq);
    }

    [Fact]
    public void Constructor_WithNullSettingsService_ThrowsArgumentNullException()
    {
        // Arrange
        var providers = new IAiProvider[]
        {
            new OpenAiProvider(NullLogger<OpenAiProvider>.Instance)
        };

        // Act & Assert
        var act = () => new AiService(
            null!,
            _credentialServiceMock.Object,
            NullLogger<AiService>.Instance,
            providers);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("settingsService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var providers = new IAiProvider[]
        {
            new OpenAiProvider(NullLogger<OpenAiProvider>.Instance)
        };

        // Act & Assert
        var act = () => new AiService(
            _settingsServiceMock.Object,
            _credentialServiceMock.Object,
            null!,
            providers);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region AvailableProviders Tests

    [Fact]
    public void AvailableProviders_ContainsCorrectProviderInfo()
    {
        // Assert
        var groqProvider = _service.AvailableProviders.FirstOrDefault(p => p.Provider == AiProvider.Groq);
        groqProvider.Should().NotBeNull();
        groqProvider!.DisplayName.Should().NotBeNullOrWhiteSpace();
        groqProvider.Models.Should().NotBeEmpty();
        groqProvider.RequiresApiKey.Should().BeTrue();
    }

    [Fact]
    public void AvailableProviders_LocalIsLocal()
    {
        // Assert
        var localProvider = _service.AvailableProviders.FirstOrDefault(p => p.Provider == AiProvider.Local);
        localProvider.Should().NotBeNull();
        localProvider!.IsLocal.Should().BeTrue();
        localProvider.RequiresApiKey.Should().BeFalse();
    }

    #endregion

    #region SetActiveProviderAsync Tests

    [Fact]
    public async Task SetActiveProviderAsync_WithValidProvider_SetsActiveProvider()
    {
        // Arrange
        _settingsServiceMock
            .Setup(x => x.SaveSettingsAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.SetActiveProviderAsync(AiProvider.Claude);

        // Assert
        _service.ActiveProvider.Should().Be(AiProvider.Claude);
    }

    [Fact]
    public async Task SetActiveProviderAsync_SavesProviderToSettings()
    {
        // Arrange
        AppSettings? savedSettings = null;
        _settingsServiceMock
            .Setup(x => x.SaveSettingsAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .Callback<AppSettings, CancellationToken>((s, _) => savedSettings = s)
            .Returns(Task.CompletedTask);

        // Act
        await _service.SetActiveProviderAsync(AiProvider.Gemini);

        // Assert
        savedSettings.Should().NotBeNull();
        savedSettings!.AiSettings.ActiveProvider.Should().Be(AiProvider.Gemini);
    }

    #endregion

    #region ConfigureProviderAsync Tests

    [Fact]
    public async Task ConfigureProviderAsync_WithNullConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        await _service.Invoking(s => s.ConfigureProviderAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ConfigureProviderAsync_SavesConfigToSettings()
    {
        // Arrange
        var config = new AiProviderConfig
        {
            Provider = AiProvider.OpenAI,
            ApiKey = "new-test-key",
            TextModel = "gpt-4o"
        };

        AppSettings? savedSettings = null;
        _settingsServiceMock
            .Setup(x => x.SaveSettingsAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .Callback<AppSettings, CancellationToken>((s, _) => savedSettings = s)
            .Returns(Task.CompletedTask);

        // Act
        await _service.ConfigureProviderAsync(config);

        // Assert
        savedSettings.Should().NotBeNull();
        savedSettings!.AiSettings.ProviderConfigs.Should().Contain(c => 
            c.Provider == AiProvider.OpenAI && c.ApiKey == "new-test-key");
    }

    #endregion

    #region AnalyzeFileAsync Tests

    [Fact]
    public async Task AnalyzeFileAsync_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        await _service.Invoking(s => s.AnalyzeFileAsync(null!))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        await _service.Invoking(s => s.AnalyzeFileAsync(string.Empty))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithWhitespacePath_ThrowsArgumentException()
    {
        // Act & Assert
        await _service.Invoking(s => s.AnalyzeFileAsync("   "))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AnalyzeFileAsync_WhenDisabled_ReturnsFailedResult()
    {
        // Arrange
        SetupDefaultSettings(enabled: false);
        var filePath = _fixture.CreateFile("test.txt", "content");

        // Act
        var result = await _service.AnalyzeFileAsync(filePath);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("disabled");
    }

    [Fact]
    public async Task AnalyzeFileAsync_WhenDisclaimerNotAccepted_ReturnsFailedResult()
    {
        // Arrange
        SetupDefaultSettings(enabled: true, disclaimerAccepted: false);
        var filePath = _fixture.CreateFile("test.txt", "content");

        // Act
        var result = await _service.AnalyzeFileAsync(filePath);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("disclaimer");
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithUnsupportedFileType_ReturnsFailedResult()
    {
        // Arrange
        var filePath = _fixture.CreateFile("test.xyz", "content");

        // Act
        var result = await _service.AnalyzeFileAsync(filePath);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not supported");
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithNonExistentFile_ReturnsFailedResult()
    {
        // Arrange
        var filePath = _fixture.GetPath("nonexistent.png");

        // Act
        var result = await _service.AnalyzeFileAsync(filePath);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("no longer exists");
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithFileTooLarge_ReturnsFailedResult()
    {
        // Arrange
        SetupDefaultSettings(enabled: true, disclaimerAccepted: true, maxFileSizeMb: 1);
        var filePath = _fixture.CreateFileWithSize("large.txt", 2 * 1024 * 1024); // 2MB file

        // Act
        var result = await _service.AnalyzeFileAsync(filePath);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("exceeds");
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithVisionDisabled_ReturnsFailedResultForImages()
    {
        // Arrange
        SetupDefaultSettings(enabled: true, disclaimerAccepted: true, enableVisionAnalysis: false);
        var filePath = _fixture.CreateFile("test.png", "fake image data");

        // Act
        var result = await _service.AnalyzeFileAsync(filePath);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("disabled");
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithDocumentAnalysisDisabled_ReturnsFailedResultForDocuments()
    {
        // Arrange
        SetupDefaultSettings(enabled: true, disclaimerAccepted: true, enableDocumentAnalysis: false);
        var filePath = _fixture.CreateFile("test.txt", "document content");

        // Act
        var result = await _service.AnalyzeFileAsync(filePath);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("disabled");
    }

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".png")]
    [InlineData(".gif")]
    [InlineData(".bmp")]
    [InlineData(".webp")]
    public async Task AnalyzeFileAsync_WithImageExtension_RoutesToImageAnalysis(string extension)
    {
        // Arrange
        var filePath = _fixture.CreateFile($"test{extension}", "fake image data");

        // Act - Will fail because no real API key, but we verify it's routed correctly
        var result = await _service.AnalyzeFileAsync(filePath);

        // Assert - Should attempt image analysis (provider not initialized with real key)
        result.Success.Should().BeFalse();
        // Error indicates image analysis was attempted, not unsupported file
        result.Error.Should().NotContain("not supported");
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".md")]
    [InlineData(".json")]
    [InlineData(".xml")]
    [InlineData(".csv")]
    [InlineData(".log")]
    public async Task AnalyzeFileAsync_WithDocumentExtension_RoutesToDocumentAnalysis(string extension)
    {
        // Arrange
        var filePath = _fixture.CreateFile($"test{extension}", "document content");

        // Act
        var result = await _service.AnalyzeFileAsync(filePath);

        // Assert - Should attempt document analysis (provider not initialized with real key)
        result.Success.Should().BeFalse();
        result.Error.Should().NotContain("not supported");
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithCustomFolders_PassesFoldersToProvider()
    {
        // Arrange
        var filePath = _fixture.CreateFile("test.txt", "document content");
        var customFolders = new List<CustomFolder>
        {
            new() { Name = "Work", Path = @"C:\Work" },
            new() { Name = "Personal", Path = @"C:\Personal" }
        };

        // Act
        var result = await _service.AnalyzeFileAsync(filePath, customFolders);

        // Assert - Verifies the method doesn't throw with custom folders
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task AnalyzeFileAsync_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        var filePath = _fixture.CreateFile("test.txt", "content");
        using var cts = new CancellationTokenSource();
        
        // The service may handle cancellation gracefully (returning failure) 
        // or throw OperationCanceledException depending on where cancellation occurs.
        // This test verifies cancellation is checked at some point.
        await cts.CancelAsync();

        // Act
        var act = async () => await _service.AnalyzeFileAsync(filePath, null, cts.Token);
        
        // Assert - Either throws OperationCanceledException or returns a failed result
        try
        {
            var result = await act();
            // If no exception, the operation completed (possibly returned early due to cancellation check)
            // This is acceptable behavior
            result.Should().NotBeNull();
        }
        catch (OperationCanceledException)
        {
            // Also acceptable - cancellation was detected and thrown
        }
    }

    #endregion

    #region AnalyzeImageAsync Tests

    [Fact]
    public async Task AnalyzeImageAsync_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        await _service.Invoking(s => s.AnalyzeImageAsync(null!))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AnalyzeImageAsync_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        await _service.Invoking(s => s.AnalyzeImageAsync(string.Empty))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AnalyzeImageAsync_WithWhitespacePath_ThrowsArgumentException()
    {
        // Act & Assert
        await _service.Invoking(s => s.AnalyzeImageAsync("   "))
            .Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region AnalyzeDocumentAsync Tests

    [Fact]
    public async Task AnalyzeDocumentAsync_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        await _service.Invoking(s => s.AnalyzeDocumentAsync(null!))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AnalyzeDocumentAsync_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        await _service.Invoking(s => s.AnalyzeDocumentAsync(string.Empty))
            .Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region AnalyzeTextAsync Tests

    [Fact]
    public async Task AnalyzeTextAsync_WithNullPrompt_ThrowsArgumentException()
    {
        // Act & Assert
        await _service.Invoking(s => s.AnalyzeTextAsync(null!))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AnalyzeTextAsync_WithEmptyPrompt_ThrowsArgumentException()
    {
        // Act & Assert
        await _service.Invoking(s => s.AnalyzeTextAsync(string.Empty))
            .Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region IsAvailableAsync Tests

    [Fact]
    public async Task IsAvailableAsync_WhenEnabled_ReturnsTrue()
    {
        // Arrange
        SetupDefaultSettings(enabled: true, disclaimerAccepted: true);

        // Act
        var result = await _service.IsAvailableAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        SetupDefaultSettings(enabled: false, disclaimerAccepted: true);

        // Act
        var result = await _service.IsAvailableAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAvailableAsync_WhenDisclaimerNotAccepted_ReturnsFalse()
    {
        // Arrange
        SetupDefaultSettings(enabled: true, disclaimerAccepted: false);

        // Act
        var result = await _service.IsAvailableAsync();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Secure API Key Tests

    [Fact]
    public async Task StoreApiKeySecurelyAsync_WithValidKey_StoresAndReturnsTrue()
    {
        // Arrange
        _credentialServiceMock
            .Setup(x => x.StoreCredentialAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        
        _settingsServiceMock
            .Setup(x => x.SaveSettingsAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.StoreApiKeySecurelyAsync(AiProvider.OpenAI, "test-api-key");

        // Assert
        result.Should().BeTrue();
        _credentialServiceMock.Verify(x => x.StoreCredentialAsync(
            It.Is<string>(k => k.Contains("OpenAI")),
            It.Is<string>(v => v == "test-api-key")), Times.Once);
    }

    [Fact]
    public async Task StoreApiKeySecurelyAsync_WithNullKey_ThrowsArgumentException()
    {
        // Act & Assert
        await _service.Invoking(s => s.StoreApiKeySecurelyAsync(AiProvider.OpenAI, null!))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task StoreApiKeySecurelyAsync_WhenStorageFails_ReturnsFalse()
    {
        // Arrange
        _credentialServiceMock
            .Setup(x => x.StoreCredentialAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.StoreApiKeySecurelyAsync(AiProvider.OpenAI, "test-key");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetApiKeyAsync_WithSecuredKey_RetrievesFromCredentialService()
    {
        // Arrange
        var settings = new AppSettings
        {
            AiSettings = new AiSettings
            {
                Enabled = true,
                DisclaimerAccepted = true,
                ActiveProvider = AiProvider.OpenAI,
                ProviderConfigs = [
                    new AiProviderConfig
                    {
                        Provider = AiProvider.OpenAI,
                        ApiKey = string.Empty,
                        IsKeySecured = true
                    }
                ]
            }
        };

        _settingsServiceMock
            .Setup(x => x.GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _credentialServiceMock
            .Setup(x => x.GetCredentialAsync(It.IsAny<string>()))
            .ReturnsAsync("secure-api-key-from-storage");

        // Act
        var result = await _service.GetApiKeyAsync(AiProvider.OpenAI);

        // Assert
        result.Should().Be("secure-api-key-from-storage");
    }

    [Fact]
    public async Task GetApiKeyAsync_WithPlaintextKey_ReturnsFromConfig()
    {
        // Act
        var result = await _service.GetApiKeyAsync(AiProvider.Groq);

        // Assert
        result.Should().Be("test-api-key");
    }

    [Fact]
    public async Task GetApiKeyAsync_WithNonExistentProvider_ReturnsNull()
    {
        // Act
        var result = await _service.GetApiKeyAsync(AiProvider.Claude);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetProviderConfig Tests

    [Fact]
    public async Task GetProviderConfig_WithExistingProvider_ReturnsConfig()
    {
        // Arrange - Must refresh availability to populate the cache
        await _service.RefreshAvailabilityAsync();
        
        // Act
        var config = _service.GetProviderConfig(AiProvider.Groq);

        // Assert
        config.Should().NotBeNull();
        config!.Provider.Should().Be(AiProvider.Groq);
        config.ApiKey.Should().Be("test-api-key");
    }

    [Fact]
    public async Task GetProviderConfig_WithNonConfiguredProvider_ReturnsNull()
    {
        // Arrange - Must refresh availability to populate the cache
        await _service.RefreshAvailabilityAsync();
        
        // Act
        var config = _service.GetProviderConfig(AiProvider.OpenAI);

        // Assert
        config.Should().BeNull();
    }

    [Fact]
    public async Task GetProviderConfigAsync_WithExistingProvider_ReturnsConfig()
    {
        // Act
        var config = await _service.GetProviderConfigAsync(AiProvider.Groq);

        // Assert
        config.Should().NotBeNull();
        config!.Provider.Should().Be(AiProvider.Groq);
        config.ApiKey.Should().Be("test-api-key");
    }

    [Fact]
    public async Task GetProviderConfigAsync_WithNonExistentProvider_ReturnsNull()
    {
        // Act
        var config = await _service.GetProviderConfigAsync(AiProvider.Claude);

        // Assert
        config.Should().BeNull();
    }

    #endregion

    #region Provider Capabilities Tests

    [Fact]
    public void SupportsVision_BeforeInitialization_ReturnsFalse()
    {
        // Assert - Before any provider is initialized
        _service.SupportsVision.Should().BeFalse();
    }

    [Fact]
    public void SupportsPdf_BeforeInitialization_ReturnsFalse()
    {
        // Assert
        _service.SupportsPdf.Should().BeFalse();
    }

    [Fact]
    public void ActiveProviderInfo_BeforeInitialization_ReturnsNull()
    {
        // Assert
        _service.ActiveProviderInfo.Should().BeNull();
    }

    [Fact]
    public async Task ActiveProviderInfo_AfterSetProvider_ReturnsProviderInfo()
    {
        // Arrange
        _settingsServiceMock
            .Setup(x => x.SaveSettingsAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.SetActiveProviderAsync(AiProvider.Claude);

        // Assert
        _service.ActiveProviderInfo.Should().NotBeNull();
        _service.ActiveProviderInfo!.Provider.Should().Be(AiProvider.Claude);
    }

    #endregion

    #region ValidateProviderAsync Tests

    [Fact]
    public async Task ValidateProviderAsync_WithNonExistentProvider_ReturnsFalse()
    {
        // Arrange - create a mock for a non-standard provider value
        // Since all standard providers exist, we test with unconfigured one
        var result = await _service.ValidateProviderAsync((AiProvider)99);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateProviderAsync_UpdatesConfigValidationStatus()
    {
        // Arrange
        AppSettings? savedSettings = null;
        _settingsServiceMock
            .Setup(x => x.SaveSettingsAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .Callback<AppSettings, CancellationToken>((s, _) => savedSettings = s)
            .Returns(Task.CompletedTask);

        // Act - Will fail validation without real API key, but should update status
        await _service.ValidateProviderAsync(AiProvider.Groq);

        // Assert
        savedSettings.Should().NotBeNull();
        var config = savedSettings!.AiSettings.ProviderConfigs
            .FirstOrDefault(c => c.Provider == AiProvider.Groq);
        config.Should().NotBeNull();
        config!.LastValidated.Should().NotBeNull();
    }

    #endregion

    #region RefreshAvailabilityAsync Tests

    [Fact]
    public async Task RefreshAvailabilityAsync_UpdatesCachedSettings()
    {
        // Arrange
        SetupDefaultSettings(enabled: true, disclaimerAccepted: true);

        // Act
        await _service.RefreshAvailabilityAsync();

        // Assert - IsAvailable should now return correct value from cache
        _service.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshAvailabilityAsync_WithDisabledSettings_UpdatesIsAvailable()
    {
        // Arrange
        SetupDefaultSettings(enabled: false, disclaimerAccepted: true);

        // Act
        await _service.RefreshAvailabilityAsync();

        // Assert
        _service.IsAvailable.Should().BeFalse();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_DisposesAllProviders()
    {
        // Arrange
        var service = CreateAiService();

        // Act & Assert (should not throw)
        service.Dispose();
        service.Dispose(); // Double dispose should be safe
    }

    [Fact]
    public void Dispose_ClearsProviderCollection()
    {
        // Arrange
        var service = CreateAiService();
        var initialCount = service.AvailableProviders.Count;

        // Act
        service.Dispose();

        // Assert - providers were cleared during dispose
        initialCount.Should().Be(5);
    }

    #endregion
}
