using AutoDrop.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoDrop.Tests.Services;

/// <summary>
/// Unit tests for FileOperationService.
/// Tests file/folder move operations, undo functionality, and unique path generation.
/// Uses real file system operations for accurate behavior testing.
/// </summary>
public sealed class FileOperationServiceTests : IDisposable
{
    private readonly TestFileFixture _fixture;
    private readonly FileOperationService _service;
    private readonly ILogger<FileOperationService> _logger;

    public FileOperationServiceTests()
    {
        _fixture = new TestFileFixture();
        _logger = NullLogger<FileOperationService>.Instance;
        _service = new FileOperationService(_logger);
    }

    public void Dispose() => _fixture.Dispose();

    #region MoveAsync - File Operations

    [Fact]
    public async Task MoveAsync_SingleFile_MovesFileToDestination()
    {
        // Arrange
        var sourceFile = _fixture.CreateFile("test.txt", "Hello World");
        var destinationDir = _fixture.CreateSubDirectory("destination");
        var originalContent = File.ReadAllText(sourceFile);

        // Act
        var result = await _service.MoveAsync(sourceFile, destinationDir);

        // Assert
        result.Should().NotBeNull();
        result.ItemName.Should().Be("test.txt");
        result.SourcePath.Should().Be(sourceFile);
        result.IsDirectory.Should().BeFalse();
        result.CanUndo.Should().BeTrue();
        
        File.Exists(sourceFile).Should().BeFalse("Source file should be removed");
        File.Exists(result.DestinationPath).Should().BeTrue("File should exist at destination");
        File.ReadAllText(result.DestinationPath).Should().Be(originalContent, "Content should be preserved");
    }

    [Fact]
    public async Task MoveAsync_FileWithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var sourceFile = _fixture.CreateFile("file with spaces & symbols (1).txt", "Special content");
        var destinationDir = _fixture.CreateSubDirectory("dest_special");

        // Act
        var result = await _service.MoveAsync(sourceFile, destinationDir);

        // Assert
        result.ItemName.Should().Be("file with spaces & symbols (1).txt");
        File.Exists(result.DestinationPath).Should().BeTrue();
    }

    [Fact]
    public async Task MoveAsync_LargeFile_MovesSuccessfully()
    {
        // Arrange - Create a 1MB file
        var sourceFile = _fixture.CreateFileWithSize("largefile.bin", 1024 * 1024);
        var destinationDir = _fixture.CreateSubDirectory("dest_large");
        var originalSize = new FileInfo(sourceFile).Length;

        // Act
        var result = await _service.MoveAsync(sourceFile, destinationDir);

        // Assert
        new FileInfo(result.DestinationPath).Length.Should().Be(originalSize);
    }

    [Fact]
    public async Task MoveAsync_FileAlreadyExistsAtDestination_CreatesUniqueFileName()
    {
        // Arrange
        var sourceFile = _fixture.CreateFile("duplicate.txt", "Source content");
        var destinationDir = _fixture.CreateSubDirectory("dest_dup");
        
        // Create existing file at destination
        File.WriteAllText(Path.Combine(destinationDir, "duplicate.txt"), "Existing content");

        // Act
        var result = await _service.MoveAsync(sourceFile, destinationDir);

        // Assert
        result.DestinationPath.Should().Contain("duplicate (1).txt");
        File.Exists(result.DestinationPath).Should().BeTrue();
        File.ReadAllText(result.DestinationPath).Should().Be("Source content");
        
        // Original at destination should be untouched
        File.ReadAllText(Path.Combine(destinationDir, "duplicate.txt")).Should().Be("Existing content");
    }

    [Fact]
    public async Task MoveAsync_MultipleConflicts_IncrementsCounter()
    {
        // Arrange
        var sourceFile = _fixture.CreateFile("conflict.txt", "New content");
        var destinationDir = _fixture.CreateSubDirectory("dest_multi_conflict");
        
        // Create multiple existing files
        File.WriteAllText(Path.Combine(destinationDir, "conflict.txt"), "Original");
        File.WriteAllText(Path.Combine(destinationDir, "conflict (1).txt"), "First");
        File.WriteAllText(Path.Combine(destinationDir, "conflict (2).txt"), "Second");

        // Act
        var result = await _service.MoveAsync(sourceFile, destinationDir);

        // Assert
        result.DestinationPath.Should().Contain("conflict (3).txt");
    }

    [Fact]
    public async Task MoveAsync_DestinationFolderDoesNotExist_CreatesFolder()
    {
        // Arrange
        var sourceFile = _fixture.CreateFile("newdest.txt", "Content");
        var destinationDir = _fixture.GetPath("nonexistent_folder");

        // Act
        var result = await _service.MoveAsync(sourceFile, destinationDir);

        // Assert
        Directory.Exists(destinationDir).Should().BeTrue();
        File.Exists(result.DestinationPath).Should().BeTrue();
    }

    [Fact]
    public async Task MoveAsync_SourceFileDoesNotExist_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentFile = _fixture.GetPath("nonexistent.txt");
        var destinationDir = _fixture.CreateSubDirectory("dest");

        // Act & Assert
        var act = async () => await _service.MoveAsync(nonExistentFile, destinationDir);
        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage("*Source file or folder not found*");
    }

    [Fact]
    public async Task MoveAsync_NullSourcePath_ThrowsArgumentException()
    {
        // Arrange
        var destinationDir = _fixture.CreateSubDirectory("dest");

        // Act & Assert
        var act = async () => await _service.MoveAsync(null!, destinationDir);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task MoveAsync_EmptySourcePath_ThrowsArgumentException()
    {
        // Arrange
        var destinationDir = _fixture.CreateSubDirectory("dest");

        // Act & Assert
        var act = async () => await _service.MoveAsync("   ", destinationDir);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task MoveAsync_NullDestination_ThrowsArgumentException()
    {
        // Arrange
        var sourceFile = _fixture.CreateFile("test.txt");

        // Act & Assert
        var act = async () => await _service.MoveAsync(sourceFile, null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task MoveAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var sourceFile = _fixture.CreateFile("cancel.txt");
        var destinationDir = _fixture.CreateSubDirectory("dest_cancel");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = async () => await _service.MoveAsync(sourceFile, destinationDir, cancellationToken: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region MoveAsync - Directory Operations

    [Fact]
    public async Task MoveAsync_Directory_MovesEntireDirectoryTree()
    {
        // Arrange
        var sourceDir = _fixture.CreateDirectoryWithFiles("source_dir", "file1.txt", "file2.txt");
        var destinationDir = _fixture.CreateSubDirectory("dest_dir_test");
        
        // Create a subdirectory with content
        var subDir = Path.Combine(sourceDir, "subdir");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "nested.txt"), "Nested content");

        // Act
        var result = await _service.MoveAsync(sourceDir, destinationDir);

        // Assert
        result.IsDirectory.Should().BeTrue();
        result.ItemName.Should().Be("source_dir");
        
        Directory.Exists(sourceDir).Should().BeFalse("Source directory should be removed");
        Directory.Exists(result.DestinationPath).Should().BeTrue();
        File.Exists(Path.Combine(result.DestinationPath, "file1.txt")).Should().BeTrue();
        File.Exists(Path.Combine(result.DestinationPath, "file2.txt")).Should().BeTrue();
        File.Exists(Path.Combine(result.DestinationPath, "subdir", "nested.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task MoveAsync_EmptyDirectory_MovesSuccessfully()
    {
        // Arrange
        var sourceDir = _fixture.CreateDirectory("empty_dir");
        var destinationDir = _fixture.CreateSubDirectory("dest_empty");

        // Act
        var result = await _service.MoveAsync(sourceDir, destinationDir);

        // Assert
        result.IsDirectory.Should().BeTrue();
        Directory.Exists(result.DestinationPath).Should().BeTrue();
        Directory.GetFileSystemEntries(result.DestinationPath).Should().BeEmpty();
    }

    [Fact]
    public async Task MoveAsync_DirectoryWithSameNameExists_CreatesUniqueName()
    {
        // Arrange
        var sourceDir = _fixture.CreateDirectoryWithFiles("dup_folder", "inside.txt");
        var destinationDir = _fixture.CreateSubDirectory("dest_dup_folder");
        
        // Create existing folder at destination
        Directory.CreateDirectory(Path.Combine(destinationDir, "dup_folder"));

        // Act
        var result = await _service.MoveAsync(sourceDir, destinationDir);

        // Assert
        result.DestinationPath.Should().Contain("dup_folder (1)");
        Directory.Exists(result.DestinationPath).Should().BeTrue();
    }

    #endregion

    #region UndoMoveAsync

    [Fact]
    public async Task UndoMoveAsync_ValidOperation_RestoresFileToOriginalLocation()
    {
        // Arrange
        var sourceFile = _fixture.CreateFile("undo_test.txt", "Original content");
        var destinationDir = _fixture.CreateSubDirectory("dest_undo");
        var originalSourcePath = sourceFile;
        
        var moveResult = await _service.MoveAsync(sourceFile, destinationDir);

        // Act
        var undoSuccess = await _service.UndoMoveAsync(moveResult);

        // Assert
        undoSuccess.Should().BeTrue();
        File.Exists(originalSourcePath).Should().BeTrue("File should be restored to original location");
        File.Exists(moveResult.DestinationPath).Should().BeFalse("File should be removed from destination");
        File.ReadAllText(originalSourcePath).Should().Be("Original content");
    }

    [Fact]
    public async Task UndoMoveAsync_Directory_RestoresEntireDirectoryTree()
    {
        // Arrange
        var sourceDir = _fixture.CreateDirectoryWithFiles("undo_dir", "file1.txt", "file2.txt");
        var destinationDir = _fixture.CreateSubDirectory("dest_undo_dir");
        var originalSourcePath = sourceDir;
        
        var moveResult = await _service.MoveAsync(sourceDir, destinationDir);

        // Act
        var undoSuccess = await _service.UndoMoveAsync(moveResult);

        // Assert
        undoSuccess.Should().BeTrue();
        Directory.Exists(originalSourcePath).Should().BeTrue();
        File.Exists(Path.Combine(originalSourcePath, "file1.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task UndoMoveAsync_DestinationNoLongerExists_ReturnsFalse()
    {
        // Arrange
        var sourceFile = _fixture.CreateFile("gone.txt", "Content");
        var destinationDir = _fixture.CreateSubDirectory("dest_gone");
        
        var moveResult = await _service.MoveAsync(sourceFile, destinationDir);
        
        // Delete the file at destination before undo
        File.Delete(moveResult.DestinationPath);

        // Act
        var undoSuccess = await _service.UndoMoveAsync(moveResult);

        // Assert
        undoSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UndoMoveAsync_SourceLocationNowOccupied_CreatesUniqueFileName()
    {
        // Arrange
        var sourceFile = _fixture.CreateFile("occupied.txt", "Original");
        var sourceDir = Path.GetDirectoryName(sourceFile)!;
        var destinationDir = _fixture.CreateSubDirectory("dest_occupied");
        
        var moveResult = await _service.MoveAsync(sourceFile, destinationDir);
        
        // Create a new file at the original location
        File.WriteAllText(sourceFile, "New occupant");

        // Act
        var undoSuccess = await _service.UndoMoveAsync(moveResult);

        // Assert
        undoSuccess.Should().BeTrue();
        File.Exists(sourceFile).Should().BeTrue("Original location should still have new file");
        File.ReadAllText(sourceFile).Should().Be("New occupant");
        
        // The restored file should have a unique name
        var files = Directory.GetFiles(sourceDir, "occupied*.txt");
        files.Should().HaveCount(2);
    }

    [Fact]
    public async Task UndoMoveAsync_OperationAlreadyUndone_ReturnsFalse()
    {
        // Arrange
        var sourceFile = _fixture.CreateFile("double_undo.txt", "Content");
        var destinationDir = _fixture.CreateSubDirectory("dest_double");
        
        var moveResult = await _service.MoveAsync(sourceFile, destinationDir);
        moveResult.CanUndo = false; // Simulate already undone

        // Act
        var undoSuccess = await _service.UndoMoveAsync(moveResult);

        // Assert
        undoSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UndoMoveAsync_SourceDirectoryDeleted_RecreatesDirectory()
    {
        // Arrange
        var subDir = _fixture.CreateSubDirectory("deletable");
        var sourceFile = Path.Combine(subDir, "restore.txt");
        File.WriteAllText(sourceFile, "Content");
        
        var destinationDir = _fixture.CreateSubDirectory("dest_recreate");
        
        var moveResult = await _service.MoveAsync(sourceFile, destinationDir);
        
        // Delete the source directory
        Directory.Delete(subDir);
        Directory.Exists(subDir).Should().BeFalse();

        // Act
        var undoSuccess = await _service.UndoMoveAsync(moveResult);

        // Assert
        undoSuccess.Should().BeTrue();
        Directory.Exists(subDir).Should().BeTrue("Directory should be recreated");
        File.Exists(sourceFile).Should().BeTrue();
    }

    [Fact]
    public async Task UndoMoveAsync_NullOperation_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _service.UndoMoveAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region GetUniqueFilePath

    [Fact]
    public void GetUniqueFilePath_NoConflict_ReturnsOriginalPath()
    {
        // Arrange
        var destinationDir = _fixture.CreateSubDirectory("unique_test");

        // Act
        var result = _service.GetUniqueFilePath(destinationDir, "newfile.txt");

        // Assert
        result.Should().Be(Path.Combine(destinationDir, "newfile.txt"));
    }

    [Fact]
    public void GetUniqueFilePath_WithConflict_ReturnsIncrementedPath()
    {
        // Arrange
        var destinationDir = _fixture.CreateSubDirectory("unique_conflict");
        File.WriteAllText(Path.Combine(destinationDir, "exists.txt"), "Content");

        // Act
        var result = _service.GetUniqueFilePath(destinationDir, "exists.txt");

        // Assert
        result.Should().Be(Path.Combine(destinationDir, "exists (1).txt"));
    }

    [Fact]
    public void GetUniqueFilePath_MultipleConflicts_FindsNextAvailable()
    {
        // Arrange
        var destinationDir = _fixture.CreateSubDirectory("unique_multi");
        File.WriteAllText(Path.Combine(destinationDir, "file.txt"), "0");
        File.WriteAllText(Path.Combine(destinationDir, "file (1).txt"), "1");
        File.WriteAllText(Path.Combine(destinationDir, "file (2).txt"), "2");

        // Act
        var result = _service.GetUniqueFilePath(destinationDir, "file.txt");

        // Assert
        result.Should().Be(Path.Combine(destinationDir, "file (3).txt"));
    }

    [Fact]
    public void GetUniqueFilePath_FileWithoutExtension_HandlesCorrectly()
    {
        // Arrange
        var destinationDir = _fixture.CreateSubDirectory("unique_noext");
        File.WriteAllText(Path.Combine(destinationDir, "README"), "Content");

        // Act
        var result = _service.GetUniqueFilePath(destinationDir, "README");

        // Assert
        result.Should().Be(Path.Combine(destinationDir, "README (1)"));
    }

    [Fact]
    public void GetUniqueFilePath_DirectoryConflict_HandlesCorrectly()
    {
        // Arrange
        var destinationDir = _fixture.CreateSubDirectory("unique_dir");
        Directory.CreateDirectory(Path.Combine(destinationDir, "folder"));

        // Act
        var result = _service.GetUniqueFilePath(destinationDir, "folder");

        // Assert
        result.Should().Be(Path.Combine(destinationDir, "folder (1)"));
    }

    #endregion

    #region Exists

    [Fact]
    public void Exists_ExistingFile_ReturnsTrue()
    {
        // Arrange
        var file = _fixture.CreateFile("exists_file.txt");

        // Act & Assert
        _service.Exists(file).Should().BeTrue();
    }

    [Fact]
    public void Exists_ExistingDirectory_ReturnsTrue()
    {
        // Arrange
        var dir = _fixture.CreateDirectory("exists_dir");

        // Act & Assert
        _service.Exists(dir).Should().BeTrue();
    }

    [Fact]
    public void Exists_NonExistent_ReturnsFalse()
    {
        // Arrange
        var path = _fixture.GetPath("nonexistent_anything");

        // Act & Assert
        _service.Exists(path).Should().BeFalse();
    }

    #endregion
}
