namespace AutoDrop.Tests.Models;

/// <summary>
/// Unit tests for FileRule model.
/// Tests validation, normalization, and property change notifications.
/// </summary>
public sealed class FileRuleTests
{
    #region Extension Validation

    [Fact]
    public void Extension_WithDot_StoresAsIs()
    {
        // Arrange & Act
        var rule = new FileRule { Extension = ".pdf", Destination = @"C:\Docs" };

        // Assert
        rule.Extension.Should().Be(".pdf");
    }

    [Fact]
    public void Extension_WithoutDot_AddsDot()
    {
        // Arrange & Act
        var rule = new FileRule { Extension = "pdf", Destination = @"C:\Docs" };

        // Assert
        rule.Extension.Should().Be(".pdf");
    }

    [Fact]
    public void Extension_UpperCase_NormalizesToLowerCase()
    {
        // Arrange & Act
        var rule = new FileRule { Extension = ".PDF", Destination = @"C:\Docs" };

        // Assert
        rule.Extension.Should().Be(".pdf");
    }

    [Fact]
    public void Extension_MixedCase_NormalizesToLowerCase()
    {
        // Arrange & Act
        var rule = new FileRule { Extension = ".PdF", Destination = @"C:\Docs" };

        // Assert
        rule.Extension.Should().Be(".pdf");
    }

    [Fact]
    public void Extension_WithWhitespace_Trims()
    {
        // Arrange & Act
        var rule = new FileRule { Extension = "  .pdf  ", Destination = @"C:\Docs" };

        // Assert
        rule.Extension.Should().Be(".pdf");
    }

    [Fact]
    public void Extension_Empty_ReturnsEmpty()
    {
        // Arrange & Act
        var rule = new FileRule { Extension = "", Destination = @"C:\Docs" };

        // Assert
        rule.Extension.Should().BeEmpty();
    }

    [Fact]
    public void Extension_InvalidCharacters_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => new FileRule { Extension = ".pd<f", Destination = @"C:\Docs" };
        act.Should().Throw<ArgumentException>()
            .WithMessage("*invalid character*");
    }

    [Fact]
    public void Extension_TooLong_ThrowsArgumentException()
    {
        // Arrange
        var longExtension = "." + new string('a', FileRule.MaxExtensionLength);

        // Act & Assert
        var act = () => new FileRule { Extension = longExtension, Destination = @"C:\Docs" };
        act.Should().Throw<ArgumentException>()
            .WithMessage("*maximum length*");
    }

    [Fact]
    public void Extension_MaxLength_Accepted()
    {
        // Arrange - exactly at max length including dot
        var maxExtension = "." + new string('a', FileRule.MaxExtensionLength - 1);

        // Act & Assert
        var act = () => new FileRule { Extension = maxExtension, Destination = @"C:\Docs" };
        act.Should().NotThrow();
    }

    #endregion

    #region Destination Validation

    [Fact]
    public void Destination_ValidPath_StoresAsIs()
    {
        // Arrange & Act
        var rule = new FileRule { Extension = ".pdf", Destination = @"C:\Users\Test\Documents" };

        // Assert
        rule.Destination.Should().Be(@"C:\Users\Test\Documents");
    }

    [Fact]
    public void Destination_WithWhitespace_Trims()
    {
        // Arrange & Act
        var rule = new FileRule { Extension = ".pdf", Destination = "  C:\\Docs  " };

        // Assert
        rule.Destination.Should().Be(@"C:\Docs");
    }

    [Fact]
    public void Destination_Empty_ReturnsEmpty()
    {
        // Arrange & Act
        var rule = new FileRule { Extension = ".pdf", Destination = "" };

        // Assert
        rule.Destination.Should().BeEmpty();
    }

    [Fact]
    public void Destination_InvalidPathCharacters_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => new FileRule { Extension = ".pdf", Destination = "C:\\Docs\0Invalid" };
        act.Should().Throw<ArgumentException>()
            .WithMessage("*invalid character*");
    }

    [Fact]
    public void Destination_TooLong_ThrowsArgumentException()
    {
        // Arrange
        var longPath = @"C:\" + new string('a', FileRule.MaxDestinationLength);

        // Act & Assert
        var act = () => new FileRule { Extension = ".pdf", Destination = longPath };
        act.Should().Throw<ArgumentException>()
            .WithMessage("*maximum length*");
    }

    #endregion

    #region Default Values

    [Fact]
    public void AutoMove_Default_IsFalse()
    {
        // Arrange & Act
        var rule = new FileRule { Extension = ".pdf", Destination = @"C:\Docs" };

        // Assert
        rule.AutoMove.Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_Default_IsTrue()
    {
        // Arrange & Act
        var rule = new FileRule { Extension = ".pdf", Destination = @"C:\Docs" };

        // Assert
        rule.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void UseCount_Default_IsZero()
    {
        // Arrange & Act
        var rule = new FileRule { Extension = ".pdf", Destination = @"C:\Docs" };

        // Assert
        rule.UseCount.Should().Be(0);
    }

    [Fact]
    public void LastUsedAt_Default_IsNull()
    {
        // Arrange & Act
        var rule = new FileRule { Extension = ".pdf", Destination = @"C:\Docs" };

        // Assert
        rule.LastUsedAt.Should().BeNull();
    }

    [Fact]
    public void CreatedAt_Default_IsCloseToNow()
    {
        // Arrange & Act
        var rule = new FileRule { Extension = ".pdf", Destination = @"C:\Docs" };

        // Assert
        rule.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void IsCustomFolder_Default_IsFalse()
    {
        // Arrange & Act
        var rule = new FileRule { Extension = ".pdf", Destination = @"C:\Docs" };

        // Assert
        rule.IsCustomFolder.Should().BeFalse();
    }

    #endregion

    #region DestinationName

    [Fact]
    public void DestinationName_ReturnsLastFolderName()
    {
        // Arrange
        var rule = new FileRule { Extension = ".pdf", Destination = @"C:\Users\Test\Documents" };

        // Act & Assert
        rule.DestinationName.Should().Be("Documents");
    }

    [Fact]
    public void DestinationName_WithTrailingSlash_ReturnsCorrectName()
    {
        // Arrange
        var rule = new FileRule { Extension = ".pdf", Destination = @"C:\Users\Test\Documents\" };

        // Act & Assert
        rule.DestinationName.Should().Be("Documents");
    }

    [Fact]
    public void DestinationName_RootDrive_ReturnsRoot()
    {
        // Arrange
        var rule = new FileRule { Extension = ".pdf", Destination = @"C:\" };

        // Act & Assert
        rule.DestinationName.Should().BeEmpty();
    }

    #endregion

    #region INotifyPropertyChanged

    [Fact]
    public void PropertyChanged_AutoMove_RaisesEvent()
    {
        // Arrange
        var rule = new FileRule { Extension = ".pdf", Destination = @"C:\Docs" };
        var changedProperties = new List<string>();
        rule.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        // Act
        rule.AutoMove = true;

        // Assert
        changedProperties.Should().Contain("AutoMove");
    }

    [Fact]
    public void PropertyChanged_SameValue_DoesNotRaiseEvent()
    {
        // Arrange
        var rule = new FileRule { Extension = ".pdf", Destination = @"C:\Docs", AutoMove = true };
        var eventRaised = false;
        rule.PropertyChanged += (_, _) => eventRaised = true;

        // Act
        rule.AutoMove = true; // Same value

        // Assert
        eventRaised.Should().BeFalse();
    }

    [Fact]
    public void PropertyChanged_UseCount_RaisesEvent()
    {
        // Arrange
        var rule = new FileRule { Extension = ".pdf", Destination = @"C:\Docs" };
        var changedProperties = new List<string>();
        rule.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        // Act
        rule.UseCount = 5;

        // Assert
        changedProperties.Should().Contain("UseCount");
    }

    [Fact]
    public void PropertyChanged_IsEnabled_RaisesEvent()
    {
        // Arrange
        var rule = new FileRule { Extension = ".pdf", Destination = @"C:\Docs" };
        var changedProperties = new List<string>();
        rule.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        // Act
        rule.IsEnabled = false;

        // Assert
        changedProperties.Should().Contain("IsEnabled");
    }

    #endregion
}

/// <summary>
/// Unit tests for DroppedItem model.
/// </summary>
public sealed class DroppedItemTests : IDisposable
{
    private readonly string _testDirectory;

    public DroppedItemTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "DroppedItemTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
        catch { }
    }

    [Fact]
    public void FromPath_File_ReturnsCorrectProperties()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.pdf");
        File.WriteAllText(filePath, "Test content");

        // Act
        var item = DroppedItem.FromPath(filePath);

        // Assert
        item.FullPath.Should().Be(filePath);
        item.Name.Should().Be("test.pdf");
        item.Extension.Should().Be(".pdf");
        item.IsDirectory.Should().BeFalse();
        item.Category.Should().Be(FileCategories.Document);
        item.Size.Should().BeGreaterThan(0);
    }

    [Fact]
    public void FromPath_Directory_ReturnsCorrectProperties()
    {
        // Arrange
        var dirPath = Path.Combine(_testDirectory, "TestFolder");
        Directory.CreateDirectory(dirPath);

        // Act
        var item = DroppedItem.FromPath(dirPath);

        // Assert
        item.FullPath.Should().Be(dirPath);
        item.Name.Should().Be("TestFolder");
        item.Extension.Should().BeEmpty();
        item.IsDirectory.Should().BeTrue();
        item.Category.Should().Be("Folder");
        item.Size.Should().Be(0);
    }

    [Fact]
    public void FromPath_ImageFile_CategorizesAsImage()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "photo.jpg");
        File.WriteAllText(filePath, "fake image");

        // Act
        var item = DroppedItem.FromPath(filePath);

        // Assert
        item.Category.Should().Be(FileCategories.Image);
    }

    [Fact]
    public void FromPath_VideoFile_CategorizesAsVideo()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "movie.mp4");
        File.WriteAllText(filePath, "fake video");

        // Act
        var item = DroppedItem.FromPath(filePath);

        // Assert
        item.Category.Should().Be(FileCategories.Video);
    }

    [Fact]
    public void FromPath_AudioFile_CategorizesAsAudio()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "song.mp3");
        File.WriteAllText(filePath, "fake audio");

        // Act
        var item = DroppedItem.FromPath(filePath);

        // Assert
        item.Category.Should().Be(FileCategories.Audio);
    }

    [Fact]
    public void FromPath_ArchiveFile_CategorizesAsArchive()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "backup.zip");
        File.WriteAllText(filePath, "fake zip");

        // Act
        var item = DroppedItem.FromPath(filePath);

        // Assert
        item.Category.Should().Be(FileCategories.Archive);
    }

    [Fact]
    public void FromPath_InstallerFile_CategorizesAsInstaller()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "setup.exe");
        File.WriteAllText(filePath, "fake exe");

        // Act
        var item = DroppedItem.FromPath(filePath);

        // Assert
        item.Category.Should().Be(FileCategories.Installer);
    }

    [Fact]
    public void FromPath_UnknownExtension_CategorizesAsUnknown()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "file.xyz");
        File.WriteAllText(filePath, "unknown");

        // Act
        var item = DroppedItem.FromPath(filePath);

        // Assert
        item.Category.Should().Be(FileCategories.Unknown);
    }

    [Fact]
    public void FromPath_NullPath_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => DroppedItem.FromPath(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromPath_EmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => DroppedItem.FromPath("   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromPath_NonExistentPath_ReturnsItemWithZeroSize()
    {
        // Arrange
        var fakePath = Path.Combine(_testDirectory, "nonexistent.txt");

        // Act
        var item = DroppedItem.FromPath(fakePath);

        // Assert
        item.Size.Should().Be(0);
        item.IsDirectory.Should().BeFalse();
    }

    [Theory]
    [InlineData(".jpg", FileCategories.Image)]
    [InlineData(".jpeg", FileCategories.Image)]
    [InlineData(".png", FileCategories.Image)]
    [InlineData(".gif", FileCategories.Image)]
    [InlineData(".pdf", FileCategories.Document)]
    [InlineData(".docx", FileCategories.Document)]
    [InlineData(".xlsx", FileCategories.Document)]
    [InlineData(".mp4", FileCategories.Video)]
    [InlineData(".mkv", FileCategories.Video)]
    [InlineData(".mp3", FileCategories.Audio)]
    [InlineData(".flac", FileCategories.Audio)]
    [InlineData(".zip", FileCategories.Archive)]
    [InlineData(".7z", FileCategories.Archive)]
    [InlineData(".exe", FileCategories.Installer)]
    [InlineData(".msi", FileCategories.Installer)]
    public void FromPath_KnownExtensions_CategorizedCorrectly(string extension, string expectedCategory)
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, $"file{extension}");
        File.WriteAllText(filePath, "test");

        // Act
        var item = DroppedItem.FromPath(filePath);

        // Assert
        item.Category.Should().Be(expectedCategory);
    }
}

/// <summary>
/// Unit tests for MoveOperation model.
/// </summary>
public sealed class MoveOperationTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        // Act
        var operation = new MoveOperation
        {
            SourcePath = @"C:\Source\file.txt",
            DestinationPath = @"C:\Dest\file.txt",
            ItemName = "file.txt"
        };

        // Assert
        operation.Id.Should().NotBeEmpty();
        operation.CanUndo.Should().BeTrue();
        operation.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        operation.IsDirectory.Should().BeFalse();
    }

    [Fact]
    public void Id_UniquePerInstance()
    {
        // Arrange & Act
        var op1 = new MoveOperation { SourcePath = "a", DestinationPath = "b", ItemName = "c" };
        var op2 = new MoveOperation { SourcePath = "a", DestinationPath = "b", ItemName = "c" };

        // Assert
        op1.Id.Should().NotBe(op2.Id);
    }

    [Fact]
    public void CanUndo_CanBeModified()
    {
        // Arrange
        var operation = new MoveOperation
        {
            SourcePath = @"C:\Source\file.txt",
            DestinationPath = @"C:\Dest\file.txt",
            ItemName = "file.txt"
        };

        // Act
        operation.CanUndo = false;

        // Assert
        operation.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void IsDirectory_WhenTrue_IndicatesFolder()
    {
        // Arrange & Act
        var operation = new MoveOperation
        {
            SourcePath = @"C:\Source\folder",
            DestinationPath = @"C:\Dest\folder",
            ItemName = "folder",
            IsDirectory = true
        };

        // Assert
        operation.IsDirectory.Should().BeTrue();
    }
}
