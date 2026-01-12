using AutoDrop.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoDrop.Tests.Services;

/// <summary>
/// Unit tests for StorageService.
/// Tests JSON serialization, file operations, and edge cases.
/// Uses real file system operations.
/// </summary>
public sealed class StorageServiceTests : IDisposable
{
    private readonly TestFileFixture _fixture;
    private readonly TestableStorageService _storageService;

    public StorageServiceTests()
    {
        _fixture = new TestFileFixture();
        _storageService = new TestableStorageService(_fixture.TestDirectory, NullLogger<StorageService>.Instance);
    }

    public void Dispose() => _fixture.Dispose();

    #region ReadJsonAsync

    [Fact]
    public async Task ReadJsonAsync_NonExistentFile_ReturnsNull()
    {
        // Arrange
        var filePath = _fixture.GetPath("nonexistent.json");

        // Act
        var result = await _storageService.ReadJsonAsync<TestData>(filePath);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ReadJsonAsync_ValidJson_DeserializesCorrectly()
    {
        // Arrange
        var filePath = _fixture.CreateFile("valid.json", """
            {
                "name": "Test",
                "value": 42,
                "items": ["a", "b", "c"]
            }
            """);

        // Act
        var result = await _storageService.ReadJsonAsync<TestData>(filePath);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test");
        result.Value.Should().Be(42);
        result.Items.Should().BeEquivalentTo(["a", "b", "c"]);
    }

    [Fact]
    public async Task ReadJsonAsync_CamelCaseJson_DeserializesCorrectly()
    {
        // Arrange
        var filePath = _fixture.CreateFile("camel.json", """
            {
                "name": "CamelTest",
                "value": 100
            }
            """);

        // Act
        var result = await _storageService.ReadJsonAsync<TestData>(filePath);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("CamelTest");
        result.Value.Should().Be(100);
    }

    [Fact]
    public async Task ReadJsonAsync_InvalidJson_ReturnsNull()
    {
        // Arrange
        var filePath = _fixture.CreateFile("invalid.json", "{ invalid json }");

        // Act
        var result = await _storageService.ReadJsonAsync<TestData>(filePath);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ReadJsonAsync_EmptyFile_ReturnsNull()
    {
        // Arrange
        var filePath = _fixture.CreateFile("empty.json", "");

        // Act
        var result = await _storageService.ReadJsonAsync<TestData>(filePath);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ReadJsonAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var filePath = _fixture.CreateFile("cancel.json", """{"name": "test"}""");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = async () => await _storageService.ReadJsonAsync<TestData>(filePath, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ReadJsonAsync_LargeFile_HandlesCorrectly()
    {
        // Arrange
        var items = Enumerable.Range(1, 1000).Select(i => $"item{i}").ToList();
        // Use camelCase JSON to match StorageService's JsonOptions
        var jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };
        var json = System.Text.Json.JsonSerializer.Serialize(new TestData 
        { 
            Name = "Large", 
            Value = 1000, 
            Items = items 
        }, jsonOptions);
        var filePath = _fixture.CreateFile("large.json", json);

        // Act
        var result = await _storageService.ReadJsonAsync<TestData>(filePath);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1000);
    }

    #endregion

    #region WriteJsonAsync

    [Fact]
    public async Task WriteJsonAsync_ValidData_WritesCorrectJson()
    {
        // Arrange
        var filePath = _fixture.GetPath("output.json");
        var data = new TestData { Name = "Output", Value = 123, Items = ["x", "y"] };

        // Act
        await _storageService.WriteJsonAsync(filePath, data);

        // Assert
        File.Exists(filePath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(filePath);
        content.Should().Contain("\"name\"");
        content.Should().Contain("Output");
        content.Should().Contain("123");
    }

    [Fact]
    public async Task WriteJsonAsync_CreatesDirectory_IfNotExists()
    {
        // Arrange
        var filePath = _fixture.GetPath("subdir/nested/output.json");
        var data = new TestData { Name = "Nested", Value = 1 };

        // Act
        await _storageService.WriteJsonAsync(filePath, data);

        // Assert
        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public async Task WriteJsonAsync_OverwritesExistingFile()
    {
        // Arrange
        var filePath = _fixture.CreateFile("overwrite.json", """{"name": "old"}""");
        var newData = new TestData { Name = "New", Value = 999 };

        // Act
        await _storageService.WriteJsonAsync(filePath, newData);

        // Assert
        var result = await _storageService.ReadJsonAsync<TestData>(filePath);
        result!.Name.Should().Be("New");
        result.Value.Should().Be(999);
    }

    [Fact]
    public async Task WriteJsonAsync_WritesIndentedJson()
    {
        // Arrange
        var filePath = _fixture.GetPath("indented.json");
        var data = new TestData { Name = "Indented", Value = 1 };

        // Act
        await _storageService.WriteJsonAsync(filePath, data);

        // Assert
        var content = await File.ReadAllTextAsync(filePath);
        content.Should().Contain("\n"); // Should have newlines for indentation
    }

    [Fact]
    public async Task WriteJsonAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var filePath = _fixture.GetPath("cancel_write.json");
        var data = new TestData { Name = "Test", Value = 1 };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = async () => await _storageService.WriteJsonAsync(filePath, data, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Round-trip

    [Fact]
    public async Task WriteAndRead_PreservesData()
    {
        // Arrange
        var filePath = _fixture.GetPath("roundtrip.json");
        var original = new TestData 
        { 
            Name = "RoundTrip Test", 
            Value = 42, 
            Items = ["first", "second", "third"] 
        };

        // Act
        await _storageService.WriteJsonAsync(filePath, original);
        var loaded = await _storageService.ReadJsonAsync<TestData>(filePath);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be(original.Name);
        loaded.Value.Should().Be(original.Value);
        loaded.Items.Should().BeEquivalentTo(original.Items);
    }

    [Fact]
    public async Task WriteAndRead_ComplexNestedData_PreservesStructure()
    {
        // Arrange
        var filePath = _fixture.GetPath("complex.json");
        var original = new NestedTestData
        {
            Id = Guid.NewGuid(),
            Data = new TestData { Name = "Nested", Value = 100, Items = ["a"] },
            Timestamp = DateTime.UtcNow
        };

        // Act
        await _storageService.WriteJsonAsync(filePath, original);
        var loaded = await _storageService.ReadJsonAsync<NestedTestData>(filePath);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(original.Id);
        loaded.Data.Name.Should().Be("Nested");
        loaded.Data.Value.Should().Be(100);
    }

    #endregion

    #region Path Properties

    [Fact]
    public void AppDataFolder_ReturnsExpectedPath()
    {
        // Assert
        _storageService.AppDataFolder.Should().Be(_fixture.TestDirectory);
    }

    [Fact]
    public void RulesFilePath_ContainsRulesJson()
    {
        // Assert
        _storageService.RulesFilePath.Should().EndWith("rules.json");
        _storageService.RulesFilePath.Should().StartWith(_fixture.TestDirectory);
    }

    [Fact]
    public void SettingsFilePath_ContainsSettingsJson()
    {
        // Assert
        _storageService.SettingsFilePath.Should().EndWith("settings.json");
        _storageService.SettingsFilePath.Should().StartWith(_fixture.TestDirectory);
    }

    [Fact]
    public void LogsFolder_ContainsLogs()
    {
        // Assert
        _storageService.LogsFolder.Should().Contain("Logs");
        _storageService.LogsFolder.Should().StartWith(_fixture.TestDirectory);
    }

    #endregion

    #region EnsureAppDataFolderExists

    [Fact]
    public void EnsureAppDataFolderExists_CreatesFolder_WhenNotExists()
    {
        // Arrange
        var newTestDir = Path.Combine(Path.GetTempPath(), "StorageTest_" + Guid.NewGuid().ToString("N"));
        var service = new TestableStorageService(newTestDir, NullLogger<StorageService>.Instance);

        try
        {
            // Pre-condition
            Directory.Exists(newTestDir).Should().BeFalse();

            // Act
            service.EnsureAppDataFolderExists();

            // Assert
            Directory.Exists(newTestDir).Should().BeTrue();
            Directory.Exists(service.LogsFolder).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(newTestDir))
                Directory.Delete(newTestDir, true);
        }
    }

    [Fact]
    public void EnsureAppDataFolderExists_DoesNotThrow_WhenExists()
    {
        // Arrange - folder already exists from constructor

        // Act & Assert
        var act = () => _storageService.EnsureAppDataFolderExists();
        act.Should().NotThrow();
    }

    #endregion
}

#region Test Data Classes

internal sealed class TestData
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public List<string> Items { get; set; } = [];
}

internal sealed class NestedTestData
{
    public Guid Id { get; set; }
    public TestData Data { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Testable version of StorageService that allows custom paths.
/// </summary>
internal sealed class TestableStorageService : AutoDrop.Services.Interfaces.IStorageService
{
    private readonly string _appDataFolder;
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };

    public TestableStorageService(string appDataFolder, Microsoft.Extensions.Logging.ILogger logger)
    {
        _appDataFolder = appDataFolder;
    }

    public string AppDataFolder => _appDataFolder;
    public string RulesFilePath => Path.Combine(_appDataFolder, "rules.json");
    public string SettingsFilePath => Path.Combine(_appDataFolder, "settings.json");
    public string LogsFolder => Path.Combine(_appDataFolder, "Logs");

    public void EnsureAppDataFolderExists()
    {
        if (!Directory.Exists(_appDataFolder))
            Directory.CreateDirectory(_appDataFolder);
        if (!Directory.Exists(LogsFolder))
            Directory.CreateDirectory(LogsFolder);
    }

    public async Task<T?> ReadJsonAsync<T>(string filePath, CancellationToken cancellationToken = default) where T : class
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            await using var stream = File.OpenRead(filePath);
            return await System.Text.Json.JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public async Task WriteJsonAsync<T>(string filePath, T data, CancellationToken cancellationToken = default) where T : class
    {
        EnsureAppDataFolderExists();
        
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        await using var stream = File.Create(filePath);
        await System.Text.Json.JsonSerializer.SerializeAsync(stream, data, JsonOptions, cancellationToken);
    }
}

#endregion
