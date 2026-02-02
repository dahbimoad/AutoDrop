using AutoDrop.Services.AI;
using AutoDrop.Services.AI.Providers;
using AutoDrop.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoDrop.Tests.Services.AI;

/// <summary>
/// Unit tests for AiService - the main AI orchestrator.
/// Tests provider management, file routing, and analysis delegation.
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
        var providers = new IAiProvider[]
        {
            new OpenAiProvider(NullLogger<OpenAiProvider>.Instance),
            new ClaudeProvider(NullLogger<ClaudeProvider>.Instance),
            new GeminiProvider(NullLogger<GeminiProvider>.Instance),
            new GroqProvider(NullLogger<GroqProvider>.Instance),
            new OllamaProvider(NullLogger<OllamaProvider>.Instance)
        };

        return new AiService(
            _settingsServiceMock.Object,
            _credentialServiceMock.Object,
            NullLogger<AiService>.Instance,
            providers);
    }

    private void SetupDefaultSettings(bool enabled = true, bool disclaimerAccepted = true, AiProvider activeProvider = AiProvider.Groq)
    {
        var settings = new AppSettings
        {
            AiSettings = new AiSettings
            {
                Enabled = enabled,
                DisclaimerAccepted = disclaimerAccepted,
                ActiveProvider = activeProvider,
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
        _service.AvailableProviders.Should().Contain(p => p.Provider == AiProvider.OpenAI);
        _service.AvailableProviders.Should().Contain(p => p.Provider == AiProvider.Claude);
        _service.AvailableProviders.Should().Contain(p => p.Provider == AiProvider.Gemini);
        _service.AvailableProviders.Should().Contain(p => p.Provider == AiProvider.Groq);
        _service.AvailableProviders.Should().Contain(p => p.Provider == AiProvider.Ollama);
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
    public void AvailableProviders_OllamaIsLocal()
    {
        // Assert
        var ollamaProvider = _service.AvailableProviders.FirstOrDefault(p => p.Provider == AiProvider.Ollama);
        ollamaProvider.Should().NotBeNull();
        ollamaProvider!.IsLocal.Should().BeTrue();
        ollamaProvider.RequiresApiKey.Should().BeFalse();
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

    #endregion
}
