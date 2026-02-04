using AutoDrop.Models;
using AutoDrop.Services.AI;
using AutoDrop.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoDrop.Tests.Services.AI;

/// <summary>
/// Unit tests for AiProviderBase functionality.
/// Tests JSON parsing, prompt building, and shared provider logic using a testable implementation.
/// </summary>
public sealed class AiProviderBaseTests : IDisposable
{
    private readonly TestFileFixture _fixture;
    private readonly TestableAiProvider _provider;

    public AiProviderBaseTests()
    {
        _fixture = new TestFileFixture();
        _provider = new TestableAiProvider(NullLogger<TestableAiProvider>.Instance);
    }

    public void Dispose()
    {
        _provider.Dispose();
        _fixture.Dispose();
    }

    #region ParseJsonAnalysis Tests

    [Fact]
    public void ParseJsonAnalysis_WithValidJson_ReturnsSuccessfulResult()
    {
        // Arrange
        var json = """
            {
                "category": "Receipts",
                "subcategory": "Shopping",
                "suggestedName": "amazon_receipt_2024",
                "description": "Amazon purchase receipt",
                "confidence": 0.95
            }
            """;

        // Act
        var result = _provider.TestParseJsonAnalysis(json, AiContentType.Document);

        // Assert
        result.Success.Should().BeTrue();
        result.Category.Should().Be("Receipts");
        result.Subcategory.Should().Be("Shopping");
        result.SuggestedName.Should().Be("amazon_receipt_2024");
        result.Description.Should().Be("Amazon purchase receipt");
        result.Confidence.Should().Be(0.95);
        result.ContentType.Should().Be(AiContentType.Document);
    }

    [Fact]
    public void ParseJsonAnalysis_WithCodeBlockWrapper_StripsWrapper()
    {
        // Arrange
        var json = """
            ```json
            {
                "category": "Photos",
                "confidence": 0.8
            }
            ```
            """;

        // Act
        var result = _provider.TestParseJsonAnalysis(json, AiContentType.Image);

        // Assert
        result.Success.Should().BeTrue();
        result.Category.Should().Be("Photos");
        result.Confidence.Should().Be(0.8);
    }

    [Fact]
    public void ParseJsonAnalysis_WithMatchedFolderId_SetsMatchedFolder()
    {
        // Arrange
        var folderId = Guid.NewGuid();
        var customFolders = new List<CustomFolder>
        {
            new() { Id = folderId, Name = "Work Documents", Path = @"C:\Work" }
        };
        var json = $$"""
            {
                "category": "Documents",
                "matchedFolderId": "{{folderId}}",
                "confidence": 0.9
            }
            """;

        // Act
        var result = _provider.TestParseJsonAnalysis(json, AiContentType.Document, customFolders);

        // Assert
        result.Success.Should().BeTrue();
        result.MatchedFolderId.Should().Be(folderId.ToString());
        result.MatchedFolderName.Should().Be("Work Documents");
        result.MatchedFolderPath.Should().Be(@"C:\Work");
        result.HasMatchedFolder.Should().BeTrue();
    }

    [Fact]
    public void ParseJsonAnalysis_WithSuggestedNewFolderPath_SetsPath()
    {
        // Arrange
        var json = """
            {
                "category": "Travel",
                "suggestedNewFolderPath": "Pictures/Travel/2024",
                "confidence": 0.85
            }
            """;

        // Act
        var result = _provider.TestParseJsonAnalysis(json, AiContentType.Image);

        // Assert
        result.Success.Should().BeTrue();
        result.SuggestedNewFolderPath.Should().Be("Pictures/Travel/2024");
        result.HasMatchedFolder.Should().BeFalse();
    }

    [Fact]
    public void ParseJsonAnalysis_WithInvalidJson_ReturnsFailedResult()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        // Act
        var result = _provider.TestParseJsonAnalysis(invalidJson, AiContentType.Document);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("parse");
    }

    [Fact]
    public void ParseJsonAnalysis_WithMissingCategory_UsesDefault()
    {
        // Arrange
        var json = """
            {
                "confidence": 0.7
            }
            """;

        // Act
        var result = _provider.TestParseJsonAnalysis(json, AiContentType.Document);

        // Assert
        result.Success.Should().BeTrue();
        result.Category.Should().Be("Unknown");
    }

    [Fact]
    public void ParseJsonAnalysis_WithConfidenceAboveOne_ClampsToOne()
    {
        // Arrange
        var json = """
            {
                "category": "Test",
                "confidence": 1.5
            }
            """;

        // Act
        var result = _provider.TestParseJsonAnalysis(json, AiContentType.Document);

        // Assert
        result.Success.Should().BeTrue();
        result.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void ParseJsonAnalysis_WithNegativeConfidence_ClampsToZero()
    {
        // Arrange
        var json = """
            {
                "category": "Test",
                "confidence": -0.5
            }
            """;

        // Act
        var result = _provider.TestParseJsonAnalysis(json, AiContentType.Document);

        // Assert
        result.Success.Should().BeTrue();
        result.Confidence.Should().Be(0.0);
    }

    [Fact]
    public void ParseJsonAnalysis_WithMissingConfidence_UsesDefault()
    {
        // Arrange
        var json = """
            {
                "category": "Test"
            }
            """;

        // Act
        var result = _provider.TestParseJsonAnalysis(json, AiContentType.Document);

        // Assert
        result.Success.Should().BeTrue();
        result.Confidence.Should().Be(0.5); // Default
    }

    #endregion

    #region ParseChatCompletionResponse Tests

    [Fact]
    public void ParseChatCompletionResponse_WithValidOpenAiFormat_ParsesCorrectly()
    {
        // Arrange
        var response = """
            {
                "choices": [
                    {
                        "message": {
                            "content": "{\"category\": \"Photos\", \"confidence\": 0.9}"
                        }
                    }
                ]
            }
            """;

        // Act
        var result = _provider.TestParseChatCompletionResponse(response, AiContentType.Image);

        // Assert
        result.Success.Should().BeTrue();
        result.Category.Should().Be("Photos");
        result.Confidence.Should().Be(0.9);
    }

    [Fact]
    public void ParseChatCompletionResponse_WithEmptyChoices_ReturnsFailedResult()
    {
        // Arrange
        var response = """
            {
                "choices": []
            }
            """;

        // Act
        var result = _provider.TestParseChatCompletionResponse(response, AiContentType.Document);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("No response");
    }

    [Fact]
    public void ParseChatCompletionResponse_WithEmptyContent_ReturnsFailedResult()
    {
        // Arrange
        var response = """
            {
                "choices": [
                    {
                        "message": {
                            "content": ""
                        }
                    }
                ]
            }
            """;

        // Act
        var result = _provider.TestParseChatCompletionResponse(response, AiContentType.Document);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Empty response");
    }

    [Fact]
    public void ParseChatCompletionResponse_WithInvalidJsonResponse_ReturnsFailedResult()
    {
        // Arrange
        var invalidResponse = "not valid json";

        // Act
        var result = _provider.TestParseChatCompletionResponse(invalidResponse, AiContentType.Document);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("parse");
    }

    #endregion

    #region BuildCustomFoldersSection Tests

    [Fact]
    public void BuildCustomFoldersSection_WithNullFolders_ReturnsEmptyString()
    {
        // Act
        var result = _provider.TestBuildCustomFoldersSection(null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildCustomFoldersSection_WithEmptyFolders_ReturnsEmptyString()
    {
        // Act
        var result = _provider.TestBuildCustomFoldersSection([]);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildCustomFoldersSection_WithFolders_IncludesFolderDetails()
    {
        // Arrange
        var folders = new List<CustomFolder>
        {
            new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Work", Path = @"C:\Work" },
            new() { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Personal", Path = @"C:\Personal" }
        };

        // Act
        var result = _provider.TestBuildCustomFoldersSection(folders);

        // Assert
        result.Should().Contain("USER'S EXISTING FOLDERS");
        result.Should().Contain("11111111-1111-1111-1111-111111111111");
        result.Should().Contain("Work");
        result.Should().Contain(@"C:\Work");
        result.Should().Contain("22222222-2222-2222-2222-222222222222");
        result.Should().Contain("Personal");
    }

    #endregion

    #region BuildImagePrompt Tests

    [Fact]
    public void BuildImagePrompt_WithoutCustomFolders_ReturnsBasicPrompt()
    {
        // Act
        var prompt = _provider.TestBuildImagePrompt(null);

        // Assert
        prompt.Should().Contain("Analyze this image");
        prompt.Should().Contain("category");
        prompt.Should().Contain("confidence");
        prompt.Should().Contain("Return ONLY valid JSON");
    }

    [Fact]
    public void BuildImagePrompt_WithCustomFolders_IncludesFolderSection()
    {
        // Arrange
        var folders = new List<CustomFolder>
        {
            new() { Name = "Photos", Path = @"C:\Photos" }
        };

        // Act
        var prompt = _provider.TestBuildImagePrompt(folders);

        // Assert
        prompt.Should().Contain("USER'S EXISTING FOLDERS");
        prompt.Should().Contain("matchedFolderId");
    }

    #endregion

    #region BuildDocumentPrompt Tests

    [Fact]
    public void BuildDocumentPrompt_WithContent_IncludesContent()
    {
        // Arrange
        var content = "Invoice #12345 - Amount: $100.00";

        // Act
        var prompt = _provider.TestBuildDocumentPrompt(content, null);

        // Assert
        prompt.Should().Contain("Analyze this document");
        prompt.Should().Contain("Invoice #12345");
        prompt.Should().Contain("$100.00");
    }

    [Fact]
    public void BuildDocumentPrompt_WithCustomFolders_IncludesFolderSection()
    {
        // Arrange
        var folders = new List<CustomFolder>
        {
            new() { Name = "Invoices", Path = @"C:\Invoices" }
        };

        // Act
        var prompt = _provider.TestBuildDocumentPrompt("test content", folders);

        // Assert
        prompt.Should().Contain("USER'S EXISTING FOLDERS");
        prompt.Should().Contain("Invoices");
    }

    #endregion

    #region GetImageMimeType Tests

    [Theory]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".jpeg", "image/jpeg")]
    [InlineData(".png", "image/png")]
    [InlineData(".gif", "image/gif")]
    [InlineData(".bmp", "image/bmp")]
    [InlineData(".webp", "image/webp")]
    [InlineData(".JPG", "image/jpeg")] // Case insensitive
    [InlineData(".PNG", "image/png")]
    public void GetImageMimeType_WithKnownExtension_ReturnsCorrectMimeType(string extension, string expectedMime)
    {
        // Act
        var mimeType = _provider.TestGetImageMimeType(extension);

        // Assert
        mimeType.Should().Be(expectedMime);
    }

    [Fact]
    public void GetImageMimeType_WithUnknownExtension_ReturnsOctetStream()
    {
        // Act
        var mimeType = _provider.TestGetImageMimeType(".xyz");

        // Assert
        mimeType.Should().Be("application/octet-stream");
    }

    #endregion

    #region Configure Tests

    [Fact]
    public void Configure_WithNullConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _provider.Configure(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Configure_WithValidConfig_SetsConfig()
    {
        // Arrange
        var config = new AiProviderConfig
        {
            Provider = AiProvider.OpenAI,
            ApiKey = "test-key"
        };

        // Act
        _provider.Configure(config);

        // Assert - Configuration was applied (no exception thrown)
        _provider.ProviderType.Should().Be(AiProvider.OpenAI);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var provider = new TestableAiProvider(NullLogger<TestableAiProvider>.Instance);

        // Act & Assert - Should not throw
        provider.Dispose();
        provider.Dispose();
    }

    #endregion
}

/// <summary>
/// Testable implementation of AiProviderBase that exposes protected methods for testing.
/// </summary>
internal sealed class TestableAiProvider : AiProviderBase
{
    public TestableAiProvider(ILogger logger) : base(logger) { }

    public override AiProvider ProviderType => AiProvider.OpenAI;
    public override AiProviderInfo ProviderInfo => new()
    {
        Provider = AiProvider.OpenAI,
        DisplayName = "Test Provider",
        Description = "For testing",
        Models = []
    };
    public override bool SupportsVision => true;
    public override bool SupportsPdf => false;

    public override Task<bool> ValidateAsync(CancellationToken ct = default) => Task.FromResult(true);

    public override Task<AiAnalysisResult> AnalyzeImageAsync(string imagePath, IReadOnlyList<CustomFolder>? customFolders = null, CancellationToken ct = default)
        => Task.FromResult(AiAnalysisResult.Failed("Not implemented for testing"));

    public override Task<AiAnalysisResult> AnalyzeDocumentAsync(string documentPath, IReadOnlyList<CustomFolder>? customFolders = null, CancellationToken ct = default)
        => Task.FromResult(AiAnalysisResult.Failed("Not implemented for testing"));

    public override Task<string> SendTextPromptAsync(string prompt, CancellationToken ct = default)
        => Task.FromResult("Test response");

    // Expose protected methods for testing
    public AiAnalysisResult TestParseJsonAnalysis(string textContent, AiContentType contentType, IReadOnlyList<CustomFolder>? customFolders = null)
        => ParseJsonAnalysis(textContent, contentType, customFolders);

    public AiAnalysisResult TestParseChatCompletionResponse(string apiResponse, AiContentType contentType, IReadOnlyList<CustomFolder>? customFolders = null)
        => ParseChatCompletionResponse(apiResponse, contentType, customFolders);

    public string TestBuildCustomFoldersSection(IReadOnlyList<CustomFolder>? customFolders)
        => BuildCustomFoldersSection(customFolders);

    public string TestBuildImagePrompt(IReadOnlyList<CustomFolder>? customFolders = null)
        => BuildImagePrompt(customFolders);

    public string TestBuildDocumentPrompt(string content, IReadOnlyList<CustomFolder>? customFolders = null)
        => BuildDocumentPrompt(content, customFolders);

    public string TestGetImageMimeType(string extension)
        => GetImageMimeType(extension);
}