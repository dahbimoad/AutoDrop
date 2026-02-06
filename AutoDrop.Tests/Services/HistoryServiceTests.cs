using AutoDrop.Models;
using AutoDrop.Services.Implementations;
using AutoDrop.Services.Interfaces;
using AutoDrop.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoDrop.Tests.Services;

/// <summary>
/// Unit tests for HistoryService.
/// Tests operation recording, retrieval, undo, and persistence.
/// </summary>
public sealed class HistoryServiceTests : IDisposable
{
    private readonly TestFileFixture _fixture;
    private readonly Mock<IStorageService> _mockStorageService;
    private readonly ILogger<HistoryService> _logger;
    private readonly string _testAppDataFolder;

    public HistoryServiceTests()
    {
        _fixture = new TestFileFixture();
        _logger = NullLogger<HistoryService>.Instance;
        _testAppDataFolder = _fixture.CreateDirectory("AppData");
        
        _mockStorageService = new Mock<IStorageService>();
        _mockStorageService.Setup(s => s.AppDataFolder).Returns(_testAppDataFolder);
    }

    public void Dispose() => _fixture.Dispose();

    private HistoryService CreateService() => new(_mockStorageService.Object, _logger);

    #region RecordOperationAsync Tests

    [Fact]
    public async Task RecordOperationAsync_ValidPaths_AddsToHistory()
    {
        // Arrange
        var service = CreateService();
        var sourcePath = _fixture.CreateFile("source.txt", "content");
        var destPath = Path.Combine(_testAppDataFolder, "dest.txt");

        // Act
        var result = await service.RecordOperationAsync(sourcePath, destPath);

        // Assert
        result.Should().NotBeNull();
        result.SourcePath.Should().Be(sourcePath);
        result.DestinationPath.Should().Be(destPath);
        result.ItemName.Should().Be("source.txt");
        result.Status.Should().Be(OperationStatus.Success);
        result.OperationType.Should().Be(OperationType.Move);
        service.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task RecordOperationAsync_WithAiConfidence_RecordsConfidence()
    {
        // Arrange
        var service = CreateService();
        var sourcePath = _fixture.CreateFile("test.pdf", "content");
        var destPath = Path.Combine(_testAppDataFolder, "test.pdf");
        const double confidence = 0.87;

        // Act
        var result = await service.RecordOperationAsync(sourcePath, destPath, OperationType.Move, confidence);

        // Assert
        result.AiConfidence.Should().Be(confidence);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RecordOperationAsync_InvalidSourcePath_ThrowsArgumentException(string? sourcePath)
    {
        // Arrange
        var service = CreateService();
        var destPath = Path.Combine(_testAppDataFolder, "dest.txt");

        // Act & Assert
        await service.Invoking(s => s.RecordOperationAsync(sourcePath!, destPath))
            .Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_EmptyHistory_ReturnsEmptyList()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GetAllAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_WithItems_ReturnsAllItems()
    {
        // Arrange
        var service = CreateService();
        await service.RecordOperationAsync(_fixture.CreateFile("a.txt", ""), Path.Combine(_testAppDataFolder, "a.txt"));
        await service.RecordOperationAsync(_fixture.CreateFile("b.txt", ""), Path.Combine(_testAppDataFolder, "b.txt"));
        await service.RecordOperationAsync(_fixture.CreateFile("c.txt", ""), Path.Combine(_testAppDataFolder, "c.txt"));

        // Act
        var result = await service.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    #endregion

    #region GetRecentAsync Tests

    [Fact]
    public async Task GetRecentAsync_WithLimit_ReturnsLimitedItems()
    {
        // Arrange
        var service = CreateService();
        for (int i = 0; i < 10; i++)
        {
            await service.RecordOperationAsync(
                _fixture.CreateFile($"file{i}.txt", ""), 
                Path.Combine(_testAppDataFolder, $"file{i}.txt"));
        }

        // Act
        var result = await service.GetRecentAsync(5);

        // Assert
        result.Should().HaveCount(5);
    }

    #endregion

    #region ClearHistoryAsync Tests

    [Fact]
    public async Task ClearHistoryAsync_WithItems_RemovesAllItems()
    {
        // Arrange
        var service = CreateService();
        await service.RecordOperationAsync(_fixture.CreateFile("a.txt", ""), Path.Combine(_testAppDataFolder, "a.txt"));
        await service.RecordOperationAsync(_fixture.CreateFile("b.txt", ""), Path.Combine(_testAppDataFolder, "b.txt"));

        // Act
        await service.ClearHistoryAsync();

        // Assert
        service.TotalCount.Should().Be(0);
    }

    #endregion

    #region OperationHistoryItem Model Tests

    [Fact]
    public void OperationHistoryItem_TimeAgo_ReturnsCorrectFormat()
    {
        // Arrange
        var recentItem = new OperationHistoryItem
        {
            SourcePath = "C:\\source.txt",
            DestinationPath = "C:\\dest.txt",
            ItemName = "test.txt",
            Timestamp = DateTime.UtcNow.AddSeconds(-30)
        };

        var hourAgoItem = new OperationHistoryItem
        {
            SourcePath = "C:\\source.txt",
            DestinationPath = "C:\\dest.txt",
            ItemName = "test.txt",
            Timestamp = DateTime.UtcNow.AddHours(-2)
        };

        // Assert
        recentItem.TimeAgo.Should().Be("Just now");
        hourAgoItem.TimeAgo.Should().Be("2h ago");
    }

    [Theory]
    [InlineData(OperationStatus.Success, "✓")]
    [InlineData(OperationStatus.Failed, "✗")]
    [InlineData(OperationStatus.Undone, "↩")]
    [InlineData(OperationStatus.Pending, "⏳")]
    public void OperationHistoryItem_StatusIcon_ReturnsCorrectIcon(OperationStatus status, string expectedIcon)
    {
        // Arrange
        var item = new OperationHistoryItem
        {
            SourcePath = "C:\\source.txt",
            DestinationPath = "C:\\dest.txt",
            ItemName = "test.txt",
            Status = status
        };

        // Assert
        item.StatusIcon.Should().Be(expectedIcon);
    }

    #endregion
}
