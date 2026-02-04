using System.Net.Http;
using AutoDrop.Core.Constants;
using AutoDrop.Models;
using AutoDrop.Services.Implementations;
using AutoDrop.Services.Interfaces;
using AutoDrop.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoDrop.Tests.Services;

/// <summary>
/// Unit tests for FolderOrganizationService.
/// Tests folder organization by various criteria: extension, category, size, date, name (AI), and content (AI).
/// Validates SanitizeFolderName, SkippedCount tracking, and AI integration.
/// </summary>
public sealed class FolderOrganizationServiceTests : IDisposable
{
    private readonly TestFileFixture _fixture;
    private readonly Mock<IFileOperationService> _fileOperationServiceMock;
    private readonly Mock<IAiService> _aiServiceMock;
    private readonly Mock<ISettingsService> _settingsServiceMock;
    private readonly FolderOrganizationService _service;

    public FolderOrganizationServiceTests()
    {
        _fixture = new TestFileFixture();
        _fileOperationServiceMock = new Mock<IFileOperationService>();
        _aiServiceMock = new Mock<IAiService>();
        _settingsServiceMock = new Mock<ISettingsService>();

        SetupDefaultMocks();

        _service = new FolderOrganizationService(
            _fileOperationServiceMock.Object,
            _aiServiceMock.Object,
            _settingsServiceMock.Object,
            NullLogger<FolderOrganizationService>.Instance);
    }

    public void Dispose() => _fixture.Dispose();

    private void SetupDefaultMocks()
    {
        // Default settings
        _settingsServiceMock
            .Setup(x => x.GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppSettings { CustomFolders = [] });

        // Default AI service - not available
        _aiServiceMock
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullFileOperationService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FolderOrganizationService(
            null!,
            _aiServiceMock.Object,
            _settingsServiceMock.Object,
            NullLogger<FolderOrganizationService>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("fileOperationService");
    }

    [Fact]
    public void Constructor_WithNullAiService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FolderOrganizationService(
            _fileOperationServiceMock.Object,
            null!,
            _settingsServiceMock.Object,
            NullLogger<FolderOrganizationService>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("aiService");
    }

    [Fact]
    public void Constructor_WithNullSettingsService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FolderOrganizationService(
            _fileOperationServiceMock.Object,
            _aiServiceMock.Object,
            null!,
            NullLogger<FolderOrganizationService>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("settingsService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FolderOrganizationService(
            _fileOperationServiceMock.Object,
            _aiServiceMock.Object,
            _settingsServiceMock.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region ValidateFolderAsync Tests

    [Fact]
    public async Task ValidateFolderAsync_WithNullPath_ThrowsArgumentException()
    {
        // Act
        var act = () => _service.ValidateFolderAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ValidateFolderAsync_WithEmptyPath_ThrowsArgumentException()
    {
        // Act
        var act = () => _service.ValidateFolderAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ValidateFolderAsync_WithNonExistentFolder_ReturnsInvalid()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_fixture.TestDirectory, "nonexistent");

        // Act
        var (isValid, error) = await _service.ValidateFolderAsync(nonExistentPath);

        // Assert
        isValid.Should().BeFalse();
        error.Should().Contain("does not exist");
    }

    [Fact]
    public async Task ValidateFolderAsync_WithEmptyFolder_ReturnsInvalid()
    {
        // Arrange
        var emptyFolder = _fixture.CreateDirectory("empty");

        // Act
        var (isValid, error) = await _service.ValidateFolderAsync(emptyFolder);

        // Assert
        isValid.Should().BeFalse();
        error.Should().Contain("empty");
    }

    [Fact]
    public async Task ValidateFolderAsync_WithFolderContainingFiles_ReturnsValid()
    {
        // Arrange
        var folder = _fixture.CreateDirectoryWithFiles("valid_folder", "file1.txt", "file2.txt");

        // Act
        var (isValid, error) = await _service.ValidateFolderAsync(folder);

        // Assert
        isValid.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public async Task ValidateFolderAsync_WithFolderContainingOnlySubdirectories_ReturnsValid()
    {
        // Arrange
        var folder = _fixture.CreateDirectory("with_subdirs");
        _fixture.CreateDirectory(Path.Combine("with_subdirs", "subdir1"));

        // Act
        var (isValid, error) = await _service.ValidateFolderAsync(folder);

        // Assert
        isValid.Should().BeTrue();
        error.Should().BeNull();
    }

    #endregion

    #region GetFileCountAsync Tests

    [Fact]
    public async Task GetFileCountAsync_WithNullPath_ThrowsArgumentException()
    {
        // Act
        var act = () => _service.GetFileCountAsync(null!, false);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetFileCountAsync_WithFilesInFolder_ReturnsCorrectCount()
    {
        // Arrange
        var folder = _fixture.CreateDirectoryWithFiles("count_test", "a.txt", "b.txt", "c.txt");

        // Act
        var count = await _service.GetFileCountAsync(folder, includeSubdirectories: false);

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public async Task GetFileCountAsync_WithSubdirectoriesIncluded_CountsAllFiles()
    {
        // Arrange
        var folder = _fixture.CreateDirectoryWithFiles("parent", "parent.txt");
        var subFolder = _fixture.CreateDirectory(Path.Combine("parent", "child"));
        _fixture.CreateFile(Path.Combine("parent", "child", "child1.txt"));
        _fixture.CreateFile(Path.Combine("parent", "child", "child2.txt"));

        // Act
        var count = await _service.GetFileCountAsync(folder, includeSubdirectories: true);

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public async Task GetFileCountAsync_WithSubdirectoriesExcluded_CountsOnlyTopLevel()
    {
        // Arrange
        var folder = _fixture.CreateDirectoryWithFiles("parent2", "parent.txt");
        _fixture.CreateDirectory(Path.Combine("parent2", "child"));
        _fixture.CreateFile(Path.Combine("parent2", "child", "child1.txt"));

        // Act
        var count = await _service.GetFileCountAsync(folder, includeSubdirectories: false);

        // Assert
        count.Should().Be(1);
    }

    #endregion

    #region GetSizeRange Tests

    [Theory]
    [InlineData(500, FileSizeRange.Tiny)]        // < 1 MB
    [InlineData(1024 * 1024 - 1, FileSizeRange.Tiny)]
    [InlineData(1024 * 1024, FileSizeRange.Small)]      // 1 MB
    [InlineData(5 * 1024 * 1024, FileSizeRange.Small)]  // 5 MB
    [InlineData(10 * 1024 * 1024, FileSizeRange.Medium)] // 10 MB
    [InlineData(50 * 1024 * 1024, FileSizeRange.Medium)] // 50 MB
    [InlineData(100 * 1024 * 1024, FileSizeRange.Large)] // 100 MB
    [InlineData(500 * 1024 * 1024, FileSizeRange.Large)] // 500 MB
    [InlineData(1024L * 1024 * 1024, FileSizeRange.Huge)]  // 1 GB
    [InlineData(2L * 1024 * 1024 * 1024, FileSizeRange.Huge)] // 2 GB
    public void GetSizeRange_WithVariousSizes_ReturnsCorrectRange(long sizeInBytes, FileSizeRange expected)
    {
        // Act
        var result = _service.GetSizeRange(sizeInBytes);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region GetSizeRangeDisplayName Tests

    [Theory]
    [InlineData(FileSizeRange.Tiny, "Tiny (< 1 MB)")]
    [InlineData(FileSizeRange.Small, "Small (1-10 MB)")]
    [InlineData(FileSizeRange.Medium, "Medium (10-100 MB)")]
    [InlineData(FileSizeRange.Large, "Large (100 MB - 1 GB)")]
    [InlineData(FileSizeRange.Huge, "Huge (> 1 GB)")]
    public void GetSizeRangeDisplayName_ReturnsCorrectDisplayName(FileSizeRange range, string expected)
    {
        // Act
        var result = _service.GetSizeRangeDisplayName(range);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region PreviewOrganizationAsync - ByExtension Tests

    [Fact]
    public async Task PreviewOrganizationAsync_ByExtension_GroupsFilesByExtension()
    {
        // Arrange
        var folder = _fixture.CreateDirectoryWithFiles("ext_test",
            "doc1.txt", "doc2.txt", "image1.jpg", "image2.png", "data.csv");

        var settings = new FolderOrganizationSettings
        {
            Criteria = OrganizationCriteria.ByExtension
        };

        // Act
        var groups = await _service.PreviewOrganizationAsync(folder, settings);

        // Assert
        groups.Should().HaveCount(4); // txt, jpg, png, csv
        groups.Should().Contain(g => g.GroupKey == "txt" && g.FileCount == 2);
        groups.Should().Contain(g => g.GroupKey == "jpg" && g.FileCount == 1);
    }

    #endregion

    #region PreviewOrganizationAsync - ByCategory Tests

    [Fact]
    public async Task PreviewOrganizationAsync_ByCategory_GroupsFilesByCategory()
    {
        // Arrange
        var folder = _fixture.CreateDirectoryWithFiles("cat_test",
            "doc.pdf", "report.docx", "photo.jpg", "video.mp4");

        var settings = new FolderOrganizationSettings
        {
            Criteria = OrganizationCriteria.ByCategory
        };

        // Act
        var groups = await _service.PreviewOrganizationAsync(folder, settings);

        // Assert - Categories are singular: Document, Image, Video (from FileCategories constants)
        groups.Should().Contain(g => g.GroupKey == "Document");
        groups.Should().Contain(g => g.GroupKey == "Image");
        groups.Should().Contain(g => g.GroupKey == "Video");
    }

    #endregion

    #region PreviewOrganizationAsync - BySize Tests

    [Fact]
    public async Task PreviewOrganizationAsync_BySize_GroupsFilesBySizeRange()
    {
        // Arrange
        var folder = _fixture.CreateDirectory("size_test");
        _fixture.CreateFileWithSize(Path.Combine("size_test", "tiny.txt"), 500);         // Tiny
        _fixture.CreateFileWithSize(Path.Combine("size_test", "small.bin"), 5 * 1024 * 1024); // Small

        var settings = new FolderOrganizationSettings
        {
            Criteria = OrganizationCriteria.BySize
        };

        // Act
        var groups = await _service.PreviewOrganizationAsync(folder, settings);

        // Assert
        groups.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    #endregion

    #region PreviewOrganizationAsync - ByName (AI) Tests

    [Fact]
    public async Task PreviewOrganizationAsync_ByName_WhenAiUnavailable_UsesFallback()
    {
        // Arrange
        var folder = _fixture.CreateDirectoryWithFiles("name_test",
            "IMG_20240101_photo.jpg", "Screenshot_app.png", "Invoice_2024.pdf");

        _aiServiceMock
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var settings = new FolderOrganizationSettings
        {
            Criteria = OrganizationCriteria.ByName
        };

        // Act
        var groups = await _service.PreviewOrganizationAsync(folder, settings);

        // Assert
        groups.Should().NotBeEmpty();
        groups.Should().Contain(g => g.GroupKey == "Photos");
        groups.Should().Contain(g => g.GroupKey == "Screenshots");
        groups.Should().Contain(g => g.GroupKey == "Invoices");
    }

    [Fact]
    public async Task PreviewOrganizationAsync_ByName_WhenAiAvailable_UsesAiCategorization()
    {
        // Arrange
        var folder = _fixture.CreateDirectoryWithFiles("ai_name_test", "random_file.jpg");

        _aiServiceMock
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _aiServiceMock
            .Setup(x => x.AnalyzeTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Travel_Photos");

        var settings = new FolderOrganizationSettings
        {
            Criteria = OrganizationCriteria.ByName,
            MaxAiFilesPerOperation = 10
        };

        // Act
        var groups = await _service.PreviewOrganizationAsync(folder, settings);

        // Assert
        groups.Should().NotBeEmpty();
        var file = groups.First().Files.First();
        file.AiCategory.Should().Be("Travel_Photos");
        file.AiConfidence.Should().BeNull(); // Name-based analysis doesn't provide confidence
    }

    [Fact]
    public async Task PreviewOrganizationAsync_ByName_RateLimitsAiCalls()
    {
        // Arrange
        var folder = _fixture.CreateDirectory("rate_limit_test");
        for (int i = 0; i < 25; i++)
        {
            _fixture.CreateFile(Path.Combine("rate_limit_test", $"file{i}.txt"));
        }

        _aiServiceMock
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _aiServiceMock
            .Setup(x => x.AnalyzeTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Documents");

        var settings = new FolderOrganizationSettings
        {
            Criteria = OrganizationCriteria.ByName,
            MaxAiFilesPerOperation = 10,
            AiDelayMs = 0 // No delay for testing
        };

        // Act
        var groups = await _service.PreviewOrganizationAsync(folder, settings);

        // Assert
        // Only 10 files should be analyzed by AI
        _aiServiceMock.Verify(
            x => x.AnalyzeTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(10));

        // The remaining 15 files should have skip reason
        var allFiles = groups.SelectMany(g => g.Files).ToList();
        allFiles.Where(f => !string.IsNullOrEmpty(f.SkipReason)).Should().HaveCount(15);
    }

    #endregion

    #region PreviewOrganizationAsync - ByContent (AI) Tests

    [Fact]
    public async Task PreviewOrganizationAsync_ByContent_WhenAiUnavailable_FallsBackToCategory()
    {
        // Arrange
        var folder = _fixture.CreateDirectoryWithFiles("content_test", "photo.jpg", "doc.pdf");

        _aiServiceMock
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var settings = new FolderOrganizationSettings
        {
            Criteria = OrganizationCriteria.ByContent
        };

        // Act
        var groups = await _service.PreviewOrganizationAsync(folder, settings);

        // Assert
        groups.Should().NotBeEmpty();
        // Should fall back to category-based organization (singular: Image, Document)
        groups.Should().Contain(g => g.GroupKey == "Image" || g.GroupKey == "Document");
    }

    [Fact]
    public async Task PreviewOrganizationAsync_ByContent_WhenAiAvailable_AnalyzesFiles()
    {
        // Arrange
        var folder = _fixture.CreateDirectoryWithFiles("content_ai_test", "photo.jpg");

        _aiServiceMock
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _aiServiceMock
            .Setup(x => x.AnalyzeFileAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<CustomFolder>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiAnalysisResult
            {
                Success = true,
                Category = "Vacation_Photos",
                Confidence = 0.95,
                SuggestedName = "beach_sunset"
            });

        var settings = new FolderOrganizationSettings
        {
            Criteria = OrganizationCriteria.ByContent,
            MaxAiFileSizeMb = 10
        };

        // Act
        var groups = await _service.PreviewOrganizationAsync(folder, settings);

        // Assert
        groups.Should().NotBeEmpty();
        var file = groups.First().Files.First();
        file.AiCategory.Should().Be("Vacation_Photos");
        file.AiConfidence.Should().Be(0.95);
    }

    [Fact]
    public async Task PreviewOrganizationAsync_ByContent_SkipsLargeFiles()
    {
        // Arrange
        var folder = _fixture.CreateDirectory("large_file_test");
        _fixture.CreateFileWithSize(Path.Combine("large_file_test", "huge.jpg"), 20 * 1024 * 1024); // 20 MB

        _aiServiceMock
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var settings = new FolderOrganizationSettings
        {
            Criteria = OrganizationCriteria.ByContent,
            MaxAiFileSizeMb = 10 // 10 MB limit
        };

        // Act
        var groups = await _service.PreviewOrganizationAsync(folder, settings);

        // Assert
        var file = groups.First().Files.First();
        file.SkipReason.Should().Contain("too large");
    }

    [Fact]
    public async Task PreviewOrganizationAsync_ByContent_UsesCustomFolders()
    {
        // Arrange
        var folder = _fixture.CreateDirectoryWithFiles("custom_folder_test", "photo.jpg");
        var folderId = Guid.NewGuid();

        var customFolders = new List<CustomFolder>
        {
            new() { Id = folderId, Name = "My Photos", Path = @"C:\Photos" }
        };

        _settingsServiceMock
            .Setup(x => x.GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppSettings { CustomFolders = customFolders });

        _aiServiceMock
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _aiServiceMock
            .Setup(x => x.AnalyzeFileAsync(
                It.IsAny<string>(),
                It.Is<IReadOnlyList<CustomFolder>?>(f => f != null && f.Count > 0),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiAnalysisResult
            {
                Success = true,
                Category = "Photos",
                Confidence = 0.9,
                MatchedFolderId = folderId.ToString(),
                MatchedFolderPath = @"C:\Photos"
            });

        var settings = new FolderOrganizationSettings
        {
            Criteria = OrganizationCriteria.ByContent
        };

        // Act
        var groups = await _service.PreviewOrganizationAsync(folder, settings);

        // Assert
        var file = groups.First().Files.First();
        file.DestinationFolder.Should().Be(@"C:\Photos");
    }

    #endregion

    #region ExecuteOrganizationAsync Tests

    [Fact]
    public async Task ExecuteOrganizationAsync_WithNullPath_ThrowsArgumentException()
    {
        // Act
        var act = () => _service.ExecuteOrganizationAsync(null!, []);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExecuteOrganizationAsync_WithNullGroups_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _service.ExecuteOrganizationAsync(_fixture.TestDirectory, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteOrganizationAsync_WithNoSelectedFiles_ReturnsFailed()
    {
        // Arrange
        var groups = new List<PlannedFolderGroup>
        {
            new()
            {
                DestinationFolder = @"C:\Dest",
                GroupKey = "Test",
                Files = [new PlannedFileMove { SourcePath = @"C:\test.txt", DestinationFolder = @"C:\Dest", IsSelected = false }]
            }
        };

        // Act
        var result = await _service.ExecuteOrganizationAsync(_fixture.TestDirectory, groups);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No files selected");
    }

    [Fact]
    public async Task ExecuteOrganizationAsync_WithSkippedFiles_TracksSkippedCount()
    {
        // Arrange
        var sourceFile = _fixture.CreateFile("selected.txt", "content");
        var destFolder = _fixture.CreateDirectory("dest");

        var groups = new List<PlannedFolderGroup>
        {
            new()
            {
                DestinationFolder = destFolder,
                GroupKey = "Test",
                Files =
                [
                    new PlannedFileMove
                    {
                        SourcePath = sourceFile,
                        DestinationFolder = destFolder,
                        IsSelected = true
                    },
                    new PlannedFileMove
                    {
                        SourcePath = @"C:\skipped1.txt",
                        DestinationFolder = destFolder,
                        IsSelected = true,
                        SkipReason = "File too large" // This makes IsSkipped = true
                    },
                    new PlannedFileMove
                    {
                        SourcePath = @"C:\skipped2.txt",
                        DestinationFolder = destFolder,
                        IsSelected = true,
                        SkipReason = "Rate limited" // This makes IsSkipped = true
                    }
                ]
            }
        };

        _fileOperationServiceMock
            .Setup(x => x.MoveAsync(sourceFile, destFolder, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MoveOperation
            {
                SourcePath = sourceFile,
                DestinationPath = Path.Combine(destFolder, "selected.txt"),
                ItemName = "selected.txt",
                IsDirectory = false,
                CanUndo = true
            });

        // Act
        var result = await _service.ExecuteOrganizationAsync(_fixture.TestDirectory, groups);

        // Assert
        result.SkippedCount.Should().Be(2); // Two files were marked with SkipReason
        result.MovedCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteOrganizationAsync_CreatesDestinationFolders()
    {
        // Arrange
        var sourceFile = _fixture.CreateFile("move_me.txt", "content");
        var destFolder = Path.Combine(_fixture.TestDirectory, "new_folder");

        var groups = new List<PlannedFolderGroup>
        {
            new()
            {
                DestinationFolder = destFolder,
                GroupKey = "Test",
                Files =
                [
                    new PlannedFileMove
                    {
                        SourcePath = sourceFile,
                        DestinationFolder = destFolder,
                        IsSelected = true
                    }
                ]
            }
        };

        _fileOperationServiceMock
            .Setup(x => x.MoveAsync(sourceFile, destFolder, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MoveOperation
            {
                SourcePath = sourceFile,
                DestinationPath = Path.Combine(destFolder, "move_me.txt"),
                ItemName = "move_me.txt",
                IsDirectory = false,
                CanUndo = true
            });

        // Act
        var result = await _service.ExecuteOrganizationAsync(_fixture.TestDirectory, groups);

        // Assert
        result.Success.Should().BeTrue();
        result.FoldersCreated.Should().BeGreaterThanOrEqualTo(1);
        Directory.Exists(destFolder).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteOrganizationAsync_HandlesFileOperationErrors()
    {
        // Arrange
        var sourceFile = _fixture.CreateFile("error_file.txt", "content");
        var destFolder = _fixture.CreateDirectory("error_dest");

        var groups = new List<PlannedFolderGroup>
        {
            new()
            {
                DestinationFolder = destFolder,
                GroupKey = "Test",
                Files =
                [
                    new PlannedFileMove
                    {
                        SourcePath = sourceFile,
                        DestinationFolder = destFolder,
                        IsSelected = true
                    }
                ]
            }
        };

        _fileOperationServiceMock
            .Setup(x => x.MoveAsync(sourceFile, destFolder, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("File in use"));

        // Act
        var result = await _service.ExecuteOrganizationAsync(_fixture.TestDirectory, groups);

        // Assert
        result.Success.Should().BeFalse();
        result.FailedCount.Should().Be(1);
        result.Errors.Should().HaveCount(1);
        result.Errors.First().Error.Should().Contain("File in use");
    }

    [Fact]
    public async Task ExecuteOrganizationAsync_ReportsCancellation()
    {
        // Arrange
        var sourceFile = _fixture.CreateFile("cancel_test.txt", "content");
        var destFolder = _fixture.CreateDirectory("cancel_dest");

        var groups = new List<PlannedFolderGroup>
        {
            new()
            {
                DestinationFolder = destFolder,
                GroupKey = "Test",
                Files =
                [
                    new PlannedFileMove
                    {
                        SourcePath = sourceFile,
                        DestinationFolder = destFolder,
                        IsSelected = true
                    }
                ]
            }
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => _service.ExecuteOrganizationAsync(_fixture.TestDirectory, groups, cancellationToken: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region SanitizeFolderName Tests (via PreviewOrganizationAsync)

    [Theory]
    [InlineData("CON")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("COM9")]
    [InlineData("LPT1")]
    [InlineData("LPT9")]
    public async Task PreviewOrganizationAsync_WithWindowsReservedNames_SanitizesCorrectly(string reservedName)
    {
        // Arrange
        var folder = _fixture.CreateDirectoryWithFiles("reserved_test", "test.txt");

        _aiServiceMock
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _aiServiceMock
            .Setup(x => x.AnalyzeTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservedName);

        var settings = new FolderOrganizationSettings
        {
            Criteria = OrganizationCriteria.ByName,
            AiDelayMs = 0
        };

        // Act
        var groups = await _service.PreviewOrganizationAsync(folder, settings);

        // Assert
        var destFolder = groups.First().DestinationFolder;
        var folderName = Path.GetFileName(destFolder);
        folderName.Should().StartWith("_");
        folderName.Should().Contain(reservedName);
    }

    [Fact]
    public async Task PreviewOrganizationAsync_WithInvalidCharacters_SanitizesCorrectly()
    {
        // Arrange
        var folder = _fixture.CreateDirectoryWithFiles("invalid_char_test", "test.txt");

        _aiServiceMock
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _aiServiceMock
            .Setup(x => x.AnalyzeTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Folder/With:Invalid*Chars?");

        var settings = new FolderOrganizationSettings
        {
            Criteria = OrganizationCriteria.ByName,
            AiDelayMs = 0
        };

        // Act
        var groups = await _service.PreviewOrganizationAsync(folder, settings);

        // Assert
        var destFolder = groups.First().DestinationFolder;
        var folderName = Path.GetFileName(destFolder);
        folderName.Should().NotContain("/");
        folderName.Should().NotContain(":");
        folderName.Should().NotContain("*");
        folderName.Should().NotContain("?");
    }

    [Fact]
    public async Task PreviewOrganizationAsync_WithTrailingDots_TrimsCorrectly()
    {
        // Arrange
        var folder = _fixture.CreateDirectoryWithFiles("trailing_dot_test", "test.txt");

        _aiServiceMock
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _aiServiceMock
            .Setup(x => x.AnalyzeTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("FolderName...");

        var settings = new FolderOrganizationSettings
        {
            Criteria = OrganizationCriteria.ByName,
            AiDelayMs = 0
        };

        // Act
        var groups = await _service.PreviewOrganizationAsync(folder, settings);

        // Assert
        var destFolder = groups.First().DestinationFolder;
        var folderName = Path.GetFileName(destFolder);
        folderName.Should().NotEndWith(".");
    }

    [Fact]
    public async Task PreviewOrganizationAsync_WithEmptyAiResponse_UsesOtherFallback()
    {
        // Arrange
        var folder = _fixture.CreateDirectoryWithFiles("empty_response_test", "test.txt");

        _aiServiceMock
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _aiServiceMock
            .Setup(x => x.AnalyzeTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("   "); // Whitespace only

        var settings = new FolderOrganizationSettings
        {
            Criteria = OrganizationCriteria.ByName,
            AiDelayMs = 0
        };

        // Act
        var groups = await _service.PreviewOrganizationAsync(folder, settings);

        // Assert
        var destFolder = groups.First().DestinationFolder;
        var folderName = Path.GetFileName(destFolder);
        folderName.Should().Be("Other");
    }

    #endregion

    #region Name Pattern Fallback Tests

    [Theory]
    [InlineData("IMG_20240101.jpg", "Photos")]
    [InlineData("DSC_0001.jpg", "Photos")]
    [InlineData("Photo_2024.jpg", "Photos")]
    [InlineData("Screenshot_2024.png", "Screenshots")]
    [InlineData("Screen_capture.png", "Screenshots")]
    [InlineData("VID_20240101.mp4", "Videos")]
    [InlineData("Video_clip.mp4", "Videos")]
    [InlineData("Invoice_2024.pdf", "Invoices")]
    [InlineData("Receipt_store.pdf", "Receipts")]
    [InlineData("Report_Q1.docx", "Reports")]
    [InlineData("Contract_signed.pdf", "Contracts")]
    [InlineData("Resume_John.docx", "Resumes")]
    [InlineData("CV_Smith.pdf", "Resumes")]
    [InlineData("WhatsApp_Image.jpg", "WhatsApp")]
    [InlineData("Scan_001.pdf", "Scans")]
    [InlineData("Document_final.docx", "Documents")]
    public async Task PreviewOrganizationAsync_ByName_WithKnownPatterns_UsesCorrectFallback(string fileName, string expectedFolder)
    {
        // Arrange
        var folder = _fixture.CreateDirectoryWithFiles("pattern_test", fileName);

        _aiServiceMock
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Force fallback

        var settings = new FolderOrganizationSettings
        {
            Criteria = OrganizationCriteria.ByName
        };

        // Act
        var groups = await _service.PreviewOrganizationAsync(folder, settings);

        // Assert
        groups.First().GroupKey.Should().Be(expectedFolder);
    }

    [Fact]
    public async Task PreviewOrganizationAsync_ByName_WithDatePattern_UsesDateNamed()
    {
        // Arrange
        var folder = _fixture.CreateDirectoryWithFiles("date_pattern_test", "2024-01-15_meeting.txt");

        _aiServiceMock
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var settings = new FolderOrganizationSettings
        {
            Criteria = OrganizationCriteria.ByName
        };

        // Act
        var groups = await _service.PreviewOrganizationAsync(folder, settings);

        // Assert
        groups.First().GroupKey.Should().Be("Date_Named");
    }

    [Fact]
    public async Task PreviewOrganizationAsync_ByName_WithUnknownPattern_UsesFirstLetterGroup()
    {
        // Arrange
        var folder = _fixture.CreateDirectoryWithFiles("letter_test", "zebra_document.txt");

        _aiServiceMock
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var settings = new FolderOrganizationSettings
        {
            Criteria = OrganizationCriteria.ByName
        };

        // Act
        var groups = await _service.PreviewOrganizationAsync(folder, settings);

        // Assert
        groups.First().GroupKey.Should().Be("_Z");
    }

    #endregion

    #region Progress Reporting Tests

    [Fact]
    public async Task PreviewOrganizationAsync_ReportsProgress()
    {
        // Arrange
        var folder = _fixture.CreateDirectoryWithFiles("progress_test", "file1.txt", "file2.txt");
        var progressReports = new List<FolderOrganizationProgress>();

        var settings = new FolderOrganizationSettings
        {
            Criteria = OrganizationCriteria.ByExtension
        };

        var progress = new Progress<FolderOrganizationProgress>(p => progressReports.Add(p));

        // Act
        await _service.PreviewOrganizationAsync(folder, settings, progress);

        // Give time for progress reports to be processed
        await Task.Delay(100);

        // Assert
        progressReports.Should().NotBeEmpty();
        progressReports.Should().Contain(p => p.Phase == "Scanning");
    }

    [Fact]
    public async Task ExecuteOrganizationAsync_ReportsProgress()
    {
        // Arrange
        var sourceFile = _fixture.CreateFile("progress_file.txt", "content");
        var destFolder = _fixture.CreateDirectory("progress_dest");
        var progressReports = new List<FolderOrganizationProgress>();

        var groups = new List<PlannedFolderGroup>
        {
            new()
            {
                DestinationFolder = destFolder,
                GroupKey = "Test",
                Files =
                [
                    new PlannedFileMove
                    {
                        SourcePath = sourceFile,
                        DestinationFolder = destFolder,
                        IsSelected = true
                    }
                ]
            }
        };

        _fileOperationServiceMock
            .Setup(x => x.MoveAsync(sourceFile, destFolder, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MoveOperation
            {
                SourcePath = sourceFile,
                DestinationPath = Path.Combine(destFolder, "progress_file.txt"),
                ItemName = "progress_file.txt",
                IsDirectory = false,
                CanUndo = true
            });

        var progress = new Progress<FolderOrganizationProgress>(p => progressReports.Add(p));

        // Act
        await _service.ExecuteOrganizationAsync(_fixture.TestDirectory, groups, progress);

        // Give time for progress reports to be processed
        await Task.Delay(100);

        // Assert
        progressReports.Should().NotBeEmpty();
        progressReports.Should().Contain(p => p.Phase == "Moving");
    }

    #endregion

    #region AI Error Handling Tests

    [Fact]
    public async Task PreviewOrganizationAsync_ByName_WhenAiThrows_UsesFallback()
    {
        // Arrange
        var folder = _fixture.CreateDirectoryWithFiles("ai_error_test", "file.txt");

        _aiServiceMock
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _aiServiceMock
            .Setup(x => x.AnalyzeTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API unavailable"));

        var settings = new FolderOrganizationSettings
        {
            Criteria = OrganizationCriteria.ByName,
            AiDelayMs = 0
        };

        // Act
        var groups = await _service.PreviewOrganizationAsync(folder, settings);

        // Assert
        groups.Should().NotBeEmpty();
        // Should use fallback pattern detection
    }

    [Fact]
    public async Task PreviewOrganizationAsync_ByContent_WhenAiThrows_RecordsSkipReason()
    {
        // Arrange
        var folder = _fixture.CreateDirectoryWithFiles("ai_content_error_test", "photo.jpg");

        _aiServiceMock
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _aiServiceMock
            .Setup(x => x.AnalyzeFileAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<CustomFolder>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Analysis failed"));

        var settings = new FolderOrganizationSettings
        {
            Criteria = OrganizationCriteria.ByContent,
            AiDelayMs = 0
        };

        // Act
        var groups = await _service.PreviewOrganizationAsync(folder, settings);

        // Assert
        var file = groups.First().Files.First();
        file.SkipReason.Should().Contain("AI analysis failed");
    }

    #endregion

    #region Max Files Limit Tests

    [Fact]
    public async Task PreviewOrganizationAsync_EnforcesMaxFilesLimit()
    {
        // Arrange
        var folder = _fixture.CreateDirectory("max_files_test");
        for (int i = 0; i < 10; i++)
        {
            _fixture.CreateFile(Path.Combine("max_files_test", $"file{i}.txt"));
        }

        var settings = new FolderOrganizationSettings
        {
            Criteria = OrganizationCriteria.ByExtension,
            MaxFilesPerOperation = 5
        };

        // Act
        var groups = await _service.PreviewOrganizationAsync(folder, settings);

        // Assert
        var totalFiles = groups.Sum(g => g.FileCount);
        totalFiles.Should().Be(5);
    }

    #endregion

    #region Hidden Files Tests

    [Fact]
    public async Task PreviewOrganizationAsync_SkipsHiddenFiles_WhenConfigured()
    {
        // Arrange
        var folder = _fixture.CreateDirectoryWithFiles("hidden_test", "visible.txt");
        var hiddenFile = _fixture.CreateFile(Path.Combine("hidden_test", "hidden.txt"));
        File.SetAttributes(hiddenFile, FileAttributes.Hidden);

        var settings = new FolderOrganizationSettings
        {
            Criteria = OrganizationCriteria.ByExtension,
            SkipHiddenFiles = true
        };

        // Act
        var groups = await _service.PreviewOrganizationAsync(folder, settings);

        // Assert
        var allFiles = groups.SelectMany(g => g.Files).ToList();
        allFiles.Should().NotContain(f => f.FileName == "hidden.txt");
    }

    #endregion

    #region Excluded Extensions Tests

    [Fact]
    public async Task PreviewOrganizationAsync_ExcludesSpecifiedExtensions()
    {
        // Arrange
        var folder = _fixture.CreateDirectoryWithFiles("exclude_ext_test", "keep.txt", "exclude.tmp", "keep2.doc");

        var settings = new FolderOrganizationSettings
        {
            Criteria = OrganizationCriteria.ByExtension,
            ExcludedExtensions = [".tmp"]
        };

        // Act
        var groups = await _service.PreviewOrganizationAsync(folder, settings);

        // Assert
        var allFiles = groups.SelectMany(g => g.Files).ToList();
        allFiles.Should().NotContain(f => f.Extension == ".tmp");
        allFiles.Should().HaveCount(2);
    }

    #endregion
}
