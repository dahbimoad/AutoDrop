using AutoDrop.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoDrop.Tests.Services;

/// <summary>
/// Unit tests for DuplicateDetectionService.
/// Tests file hash computation, duplicate detection, and various comparison methods.
/// Uses real file system operations for accurate behavior testing.
/// </summary>
public sealed class DuplicateDetectionServiceTests : IDisposable
{
    private readonly TestFileFixture _fixture;
    private readonly DuplicateDetectionService _service;
    private readonly ILogger<DuplicateDetectionService> _logger;

    public DuplicateDetectionServiceTests()
    {
        _fixture = new TestFileFixture();
        _logger = NullLogger<DuplicateDetectionService>.Instance;
        _service = new DuplicateDetectionService(_logger);
    }

    public void Dispose() => _fixture.Dispose();

    #region CheckForDuplicateAsync - Basic Operations

    [Fact]
    public async Task CheckForDuplicateAsync_IdenticalFiles_ReturnsDuplicate()
    {
        // Arrange
        const string content = "This is the same content in both files";
        var sourceFile = _fixture.CreateFile("source.txt", content);
        var destFile = _fixture.CreateFile("dest/destination.txt", content);

        // Act
        var result = await _service.CheckForDuplicateAsync(sourceFile, destFile);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.IsDuplicate.Should().BeTrue();
        result.IsExactMatch.Should().BeTrue();
        result.ComparisonMethod.Should().Be(DuplicateComparisonMethod.Hash);
        result.Source.Hash.Should().NotBeNullOrEmpty();
        result.Destination.Hash.Should().NotBeNullOrEmpty();
        result.Source.Hash.Should().Be(result.Destination.Hash);
    }

    [Fact]
    public async Task CheckForDuplicateAsync_DifferentFiles_ReturnsNoDuplicate()
    {
        // Arrange
        var sourceFile = _fixture.CreateFile("source.txt", "Source content");
        var destFile = _fixture.CreateFile("dest/destination.txt", "Different content");

        // Act
        var result = await _service.CheckForDuplicateAsync(sourceFile, destFile);

        // Assert
        result.Success.Should().BeTrue();
        result.IsDuplicate.Should().BeFalse();
        result.IsExactMatch.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForDuplicateAsync_DifferentSizes_ReturnsNoDuplicate()
    {
        // Arrange
        var sourceFile = _fixture.CreateFile("source.txt", "Short");
        var destFile = _fixture.CreateFile("dest/destination.txt", "Much longer content that has different size");

        // Act
        var result = await _service.CheckForDuplicateAsync(sourceFile, destFile);

        // Assert
        result.Success.Should().BeTrue();
        result.IsDuplicate.Should().BeFalse();
        result.ComparisonMethod.Should().Be(DuplicateComparisonMethod.SizeOnly);
    }

    [Fact]
    public async Task CheckForDuplicateAsync_DestinationDoesNotExist_ReturnsNoDuplicate()
    {
        // Arrange
        var sourceFile = _fixture.CreateFile("source.txt", "Content");
        var nonExistentDest = Path.Combine(_fixture.TestDirectory, "nonexistent.txt");

        // Act
        var result = await _service.CheckForDuplicateAsync(sourceFile, nonExistentDest);

        // Assert
        result.Success.Should().BeTrue();
        result.IsDuplicate.Should().BeFalse();
        result.ComparisonMethod.Should().Be(DuplicateComparisonMethod.None);
    }

    [Fact]
    public async Task CheckForDuplicateAsync_SourceDoesNotExist_ReturnsError()
    {
        // Arrange
        var nonExistentSource = Path.Combine(_fixture.TestDirectory, "nonexistent_source.txt");
        var destFile = _fixture.CreateFile("dest.txt", "Content");

        // Act
        var result = await _service.CheckForDuplicateAsync(nonExistentSource, destFile);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region CheckForDuplicateAsync - Same Size Different Content

    [Fact]
    public async Task CheckForDuplicateAsync_SameSizeDifferentContent_ReturnsNoDuplicate()
    {
        // Arrange - Create files with same size but different content
        var sourceFile = _fixture.CreateFile("source.txt", "AAAAAAA");
        var destFile = _fixture.CreateFile("dest.txt", "BBBBBBB");

        var sourceSize = new FileInfo(sourceFile).Length;
        var destSize = new FileInfo(destFile).Length;
        sourceSize.Should().Be(destSize, "Files should have same size for this test");

        // Act
        var result = await _service.CheckForDuplicateAsync(sourceFile, destFile);

        // Assert
        result.Success.Should().BeTrue();
        result.IsDuplicate.Should().BeFalse();
        result.IsExactMatch.Should().BeFalse();
        result.ComparisonMethod.Should().Be(DuplicateComparisonMethod.Hash);
    }

    #endregion

    #region CheckForDuplicateAsync - Empty Files

    [Fact]
    public async Task CheckForDuplicateAsync_BothEmpty_ReturnsDuplicate()
    {
        // Arrange
        var sourceFile = _fixture.CreateFile("empty_source.txt", "");
        var destFile = _fixture.CreateFile("empty_dest.txt", "");

        // Act
        var result = await _service.CheckForDuplicateAsync(sourceFile, destFile);

        // Assert
        result.Success.Should().BeTrue();
        result.IsDuplicate.Should().BeTrue();
        result.IsExactMatch.Should().BeTrue();
    }

    #endregion

    #region CheckForDuplicateAsync - Cancellation

    [Fact]
    public async Task CheckForDuplicateAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var sourceFile = _fixture.CreateFile("source.txt", "Content");
        var destFile = _fixture.CreateFile("dest.txt", "Content");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        // TaskCanceledException inherits from OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.CheckForDuplicateAsync(sourceFile, destFile, cts.Token));
    }

    #endregion

    #region ComputeFileHashAsync

    [Fact]
    public async Task ComputeFileHashAsync_ValidFile_ReturnsConsistentHash()
    {
        // Arrange
        var filePath = _fixture.CreateFile("hash_test.txt", "Test content for hashing");

        // Act
        var hash1 = await _service.ComputeFileHashAsync(filePath);
        var hash2 = await _service.ComputeFileHashAsync(filePath);

        // Assert
        hash1.Should().NotBeNullOrEmpty();
        hash2.Should().NotBeNullOrEmpty();
        hash1.Should().Be(hash2, "Same file should produce same hash");
    }

    [Fact]
    public async Task ComputeFileHashAsync_DifferentContent_ReturnsDifferentHash()
    {
        // Arrange
        var file1 = _fixture.CreateFile("file1.txt", "Content A");
        var file2 = _fixture.CreateFile("file2.txt", "Content B");

        // Act
        var hash1 = await _service.ComputeFileHashAsync(file1);
        var hash2 = await _service.ComputeFileHashAsync(file2);

        // Assert
        hash1.Should().NotBe(hash2, "Different content should produce different hash");
    }

    [Fact]
    public async Task ComputeFileHashAsync_EmptyFile_ReturnsValidHash()
    {
        // Arrange
        var emptyFile = _fixture.CreateFile("empty.txt", "");

        // Act
        var hash = await _service.ComputeFileHashAsync(emptyFile);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        // SHA256 of empty string is well-known
        hash.ToLowerInvariant().Should().Be("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
    }

    [Fact]
    public async Task ComputeFileHashAsync_NonExistentFile_ThrowsException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_fixture.TestDirectory, "nonexistent.txt");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _service.ComputeFileHashAsync(nonExistentPath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ComputeFileHashAsync_InvalidPath_ThrowsArgumentException(string? path)
    {
        // Act & Assert
        // ArgumentNullException inherits from ArgumentException
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => _service.ComputeFileHashAsync(path!));
    }

    #endregion

    #region IsEnabled Property

    [Fact]
    public void IsEnabled_DefaultValue_IsTrue()
    {
        // Assert
        _service.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_CanBeModified()
    {
        // Act
        _service.IsEnabled = false;

        // Assert
        _service.IsEnabled.Should().BeFalse();

        // Cleanup
        _service.IsEnabled = true;
    }

    #endregion

    #region MaxHashFileSizeBytes Property

    [Fact]
    public void MaxHashFileSizeBytes_HasReasonableDefault()
    {
        // Assert - Default should be 100MB
        _service.MaxHashFileSizeBytes.Should().Be(100 * 1024 * 1024);
    }

    #endregion

    #region FileComparisonInfo Population

    [Fact]
    public async Task CheckForDuplicateAsync_PopulatesFileComparisonInfo()
    {
        // Arrange
        var sourceFile = _fixture.CreateFile("source.txt", "Content for comparison");
        var destFile = _fixture.CreateFile("dest/destination.txt", "Different content here");

        // Act
        var result = await _service.CheckForDuplicateAsync(sourceFile, destFile);

        // Assert
        result.Source.Should().NotBeNull();
        result.Source.FilePath.Should().Be(sourceFile);
        result.Source.Size.Should().BeGreaterThan(0);
        // Use a wider tolerance to handle timezone differences
        result.Source.LastModified.Should().BeCloseTo(DateTime.Now, TimeSpan.FromHours(2));

        result.Destination.Should().NotBeNull();
        result.Destination.FilePath.Should().Be(destFile);
        result.Destination.Size.Should().BeGreaterThan(0);
    }

    #endregion
}
