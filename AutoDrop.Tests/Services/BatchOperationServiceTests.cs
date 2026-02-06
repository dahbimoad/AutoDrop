using AutoDrop.Services.Implementations;
using AutoDrop.Services.Interfaces;
using AutoDrop.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoDrop.Tests.Services;

/// <summary>
/// Unit tests for BatchOperationService.
/// Tests file grouping, batch move operations, progress reporting, and undo functionality.
/// </summary>
public sealed class BatchOperationServiceTests : IDisposable
{
    private readonly TestFileFixture _fixture;
    private readonly BatchOperationService _service;
    private readonly Mock<IFileOperationService> _mockFileOperationService;
    private readonly Mock<IDestinationSuggestionService> _mockSuggestionService;
    private readonly Mock<IDuplicateDetectionService> _mockDuplicateDetectionService;
    private readonly Mock<IRuleService> _mockRuleService;
    private readonly ILogger<BatchOperationService> _logger;

    public BatchOperationServiceTests()
    {
        _fixture = new TestFileFixture();
        _logger = NullLogger<BatchOperationService>.Instance;
        
        _mockFileOperationService = new Mock<IFileOperationService>();
        _mockSuggestionService = new Mock<IDestinationSuggestionService>();
        _mockDuplicateDetectionService = new Mock<IDuplicateDetectionService>();
        _mockRuleService = new Mock<IRuleService>();

        _service = new BatchOperationService(
            _mockFileOperationService.Object,
            _mockSuggestionService.Object,
            _mockDuplicateDetectionService.Object,
            _mockRuleService.Object,
            _logger);
    }

    public void Dispose() => _fixture.Dispose();

    #region GroupItemsByDestinationAsync - Grouping Logic

    [Fact]
    public async Task GroupItemsByDestinationAsync_EmptyItems_ReturnsEmptyList()
    {
        // Arrange
        var items = Array.Empty<DroppedItem>();

        // Act
        var result = await _service.GroupItemsByDestinationAsync(items);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GroupItemsByDestinationAsync_SingleItem_ReturnsSingleGroup()
    {
        // Arrange
        var filePath = _fixture.CreateFile("test.txt", "content");
        var item = DroppedItem.FromPath(filePath);

        // Act - Now groups by extension only, destination is set by UI
        var result = await _service.GroupItemsByDestinationAsync([item]);

        // Assert
        result.Should().HaveCount(1);
        result[0].Items.Should().HaveCount(1);
        result[0].Extension.Should().Be(".txt");
        // Destination is now empty - set by user in UI
        result[0].DestinationPath.Should().BeEmpty();
    }

    [Fact]
    public async Task GroupItemsByDestinationAsync_MultipleItemsSameCategory_GroupsTogether()
    {
        // Arrange
        var file1 = _fixture.CreateFile("doc1.txt", "content1");
        var file2 = _fixture.CreateFile("doc2.txt", "content2");
        var file3 = _fixture.CreateFile("doc3.txt", "content3");
        
        var items = new[]
        {
            DroppedItem.FromPath(file1),
            DroppedItem.FromPath(file2),
            DroppedItem.FromPath(file3)
        };

        // Act - Now groups by extension only
        var result = await _service.GroupItemsByDestinationAsync(items);

        // Assert
        result.Should().HaveCount(1);
        result[0].Items.Should().HaveCount(3);
        result[0].Category.Should().Be(items[0].Category);
    }

    #endregion

    #region ExecuteBatchMoveAsync - Successful Operations

    [Fact]
    public async Task ExecuteBatchMoveAsync_EmptyGroups_ReturnsSuccessWithZeroMoved()
    {
        // Arrange
        var groups = Array.Empty<BatchFileGroup>();

        // Act
        var result = await _service.ExecuteBatchMoveAsync(groups);

        // Assert
        result.Should().NotBeNull();
        result.IsFullSuccess.Should().BeTrue();
        result.SuccessCount.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteBatchMoveAsync_SingleGroup_MovesAllFiles()
    {
        // Arrange
        var file1 = _fixture.CreateFile("file1.txt", "content1");
        var file2 = _fixture.CreateFile("file2.txt", "content2");
        var destPath = _fixture.CreateSubDirectory("Destination");
        
        var group = new BatchFileGroup
        {
            Category = "Documents",
            DestinationPath = destPath,
            IsSelected = true, // Must be selected for processing
            Items = [DroppedItem.FromPath(file1), DroppedItem.FromPath(file2)]
        };

        SetupNoDuplicatesDetection();
        SetupSuccessfulMoveOperations();

        // Act
        var result = await _service.ExecuteBatchMoveAsync([group]);

        // Assert
        result.IsFullSuccess.Should().BeTrue();
        result.SuccessCount.Should().Be(2);
    }

    #endregion

    #region ExecuteBatchMoveAsync - Error Handling

    [Fact]
    public async Task ExecuteBatchMoveAsync_MoveFailure_RecordsError()
    {
        // Arrange
        var sourceFile = _fixture.CreateFile("failing.txt", "content");
        var destPath = _fixture.CreateSubDirectory("Destination");
        
        var group = new BatchFileGroup
        {
            Category = "Documents",
            DestinationPath = destPath,
            IsSelected = true, // Must be selected for processing
            Items = [DroppedItem.FromPath(sourceFile)]
        };

        SetupNoDuplicatesDetection();

        _mockFileOperationService
            .Setup(s => s.MoveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Access denied"));

        // Act
        var result = await _service.ExecuteBatchMoveAsync([group]);

        // Assert
        result.Errors.Should().NotBeEmpty();
        result.Errors[0].ErrorMessage.Should().Contain("Access denied");
    }

    [Fact]
    public async Task ExecuteBatchMoveAsync_PartialFailure_ContinuesWithOtherFiles()
    {
        // Arrange
        var file1 = _fixture.CreateFile("file1.txt", "content1");
        var file2 = _fixture.CreateFile("file2.txt", "content2");
        var destPath = _fixture.CreateSubDirectory("Destination");
        
        var group = new BatchFileGroup
        {
            Category = "Documents",
            DestinationPath = destPath,
            IsSelected = true, // Must be selected for processing
            Items = [DroppedItem.FromPath(file1), DroppedItem.FromPath(file2)]
        };

        SetupNoDuplicatesDetection();

        // First file fails, second succeeds - use sequence for different behaviors
        var callCount = 0;
        _mockFileOperationService
            .Setup(s => s.MoveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string source, string dest, string? _, CancellationToken __) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new IOException("First file locked");
                }
                return new MoveOperation
                {
                    ItemName = Path.GetFileName(source),
                    SourcePath = source,
                    DestinationPath = Path.Combine(dest, Path.GetFileName(source))
                };
            });

        // Act
        var result = await _service.ExecuteBatchMoveAsync([group]);

        // Assert
        result.SuccessCount.Should().Be(1);
        result.Errors.Should().HaveCount(1);
    }

    #endregion

    #region UndoBatchAsync

    [Fact]
    public async Task UndoBatchAsync_EmptyOperations_ReturnsZero()
    {
        // Arrange
        var operations = Array.Empty<MoveOperation>();

        // Act
        var result = await _service.UndoBatchAsync(operations);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task UndoBatchAsync_ValidOperations_CallsUndoForEach()
    {
        // Arrange
        var operations = new[]
        {
            new MoveOperation { ItemName = "test1.txt", SourcePath = "source1", DestinationPath = "dest1", CanUndo = true },
            new MoveOperation { ItemName = "test2.txt", SourcePath = "source2", DestinationPath = "dest2", CanUndo = true }
        };

        _mockFileOperationService
            .Setup(s => s.UndoMoveAsync(It.IsAny<MoveOperation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.UndoBatchAsync(operations);

        // Assert
        result.Should().Be(2);
    }

    #endregion

    #region Helper Methods

    private void SetupSuggestionForItem(DroppedItem item, string destinationPath)
    {
        _mockSuggestionService
            .Setup(s => s.GetSuggestionsAsync(
                It.Is<DroppedItem>(i => i.FullPath == item.FullPath), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new DestinationSuggestion
                {
                    FullPath = destinationPath,
                    DisplayName = Path.GetFileName(destinationPath),
                    Confidence = 100
                }
            ]);
    }

    private void SetupNoDuplicatesDetection()
    {
        _mockDuplicateDetectionService
            .Setup(s => s.CheckForDuplicateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DuplicateCheckResult
            {
                IsDuplicate = false,
                IsExactMatch = false,
                ComparisonMethod = DuplicateComparisonMethod.Hash
            });
    }

    private void SetupSuccessfulMoveOperations()
    {
        _mockFileOperationService
            .Setup(s => s.MoveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string source, string dest, string? _, CancellationToken __) => new MoveOperation
            {
                ItemName = Path.GetFileName(source),
                SourcePath = source,
                DestinationPath = Path.Combine(dest, Path.GetFileName(source)),
                CanUndo = true
            });
    }

    #endregion
}
