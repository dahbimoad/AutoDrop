using AutoDrop.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoDrop.Tests.Services;

/// <summary>
/// Unit tests for RuleService.
/// Tests rule CRUD operations, auto-move settings, usage tracking, and thread safety.
/// Uses real file system for persistence testing.
/// </summary>
public sealed class RuleServiceTests : IDisposable
{
    private readonly TestFileFixture _fixture;
    private readonly IStorageService _storageService;
    private readonly RuleService _ruleService;

    public RuleServiceTests()
    {
        _fixture = new TestFileFixture();
        _storageService = new TestStorageService(_fixture.TestDirectory);
        _ruleService = new RuleService(
            _storageService, 
            NullLogger<RuleService>.Instance);
    }

    public void Dispose()
    {
        _ruleService.Dispose();
        _fixture.Dispose();
    }

    #region GetAllRulesAsync

    [Fact]
    public async Task GetAllRulesAsync_EmptyConfiguration_ReturnsEmptyList()
    {
        // Act
        var rules = await _ruleService.GetAllRulesAsync();

        // Assert
        rules.Should().NotBeNull();
        rules.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllRulesAsync_WithRules_ReturnsAllRules()
    {
        // Arrange
        await _ruleService.SaveRuleAsync(".pdf", @"C:\Documents");
        await _ruleService.SaveRuleAsync(".jpg", @"C:\Pictures");
        await _ruleService.SaveRuleAsync(".mp4", @"C:\Videos");

        // Act
        var rules = await _ruleService.GetAllRulesAsync();

        // Assert
        rules.Should().HaveCount(3);
        rules.Select(r => r.Extension).Should().Contain(".pdf", ".jpg", ".mp4");
    }

    [Fact]
    public async Task GetAllRulesAsync_ReturnsReadOnlyList()
    {
        // Arrange
        await _ruleService.SaveRuleAsync(".txt", @"C:\Docs");

        // Act
        var rules = await _ruleService.GetAllRulesAsync();

        // Assert
        rules.Should().BeAssignableTo<IReadOnlyList<FileRule>>();
    }

    #endregion

    #region GetRuleForExtensionAsync

    [Fact]
    public async Task GetRuleForExtensionAsync_ExistingEnabledRule_ReturnsRule()
    {
        // Arrange
        await _ruleService.SaveRuleAsync(".pdf", @"C:\Documents", autoMove: true);

        // Act
        var rule = await _ruleService.GetRuleForExtensionAsync(".pdf");

        // Assert
        rule.Should().NotBeNull();
        rule!.Extension.Should().Be(".pdf");
        rule.Destination.Should().Be(@"C:\Documents");
        rule.AutoMove.Should().BeTrue();
    }

    [Fact]
    public async Task GetRuleForExtensionAsync_DisabledRule_ReturnsNull()
    {
        // Arrange
        await _ruleService.SaveRuleAsync(".pdf", @"C:\Documents");
        await _ruleService.SetRuleEnabledAsync(".pdf", false);

        // Act
        var rule = await _ruleService.GetRuleForExtensionAsync(".pdf");

        // Assert
        rule.Should().BeNull("Disabled rules should not be returned");
    }

    [Fact]
    public async Task GetRuleForExtensionAsync_NonExistentExtension_ReturnsNull()
    {
        // Act
        var rule = await _ruleService.GetRuleForExtensionAsync(".xyz");

        // Assert
        rule.Should().BeNull();
    }

    [Fact]
    public async Task GetRuleForExtensionAsync_CaseInsensitive_ReturnsRule()
    {
        // Arrange
        await _ruleService.SaveRuleAsync(".PDF", @"C:\Documents");

        // Act
        var rule1 = await _ruleService.GetRuleForExtensionAsync(".pdf");
        var rule2 = await _ruleService.GetRuleForExtensionAsync(".PDF");
        var rule3 = await _ruleService.GetRuleForExtensionAsync(".Pdf");

        // Assert
        rule1.Should().NotBeNull();
        rule2.Should().NotBeNull();
        rule3.Should().NotBeNull();
    }

    [Fact]
    public async Task GetRuleForExtensionAsync_WithoutDot_NormalizesExtension()
    {
        // Arrange
        await _ruleService.SaveRuleAsync("pdf", @"C:\Documents");

        // Act
        var rule = await _ruleService.GetRuleForExtensionAsync(".pdf");

        // Assert
        rule.Should().NotBeNull();
        rule!.Extension.Should().Be(".pdf");
    }

    #endregion

    #region SaveRuleAsync

    [Fact]
    public async Task SaveRuleAsync_NewRule_CreatesRule()
    {
        // Act
        var rule = await _ruleService.SaveRuleAsync(".docx", @"C:\Documents", autoMove: true);

        // Assert
        rule.Should().NotBeNull();
        rule.Extension.Should().Be(".docx");
        rule.Destination.Should().Be(@"C:\Documents");
        rule.AutoMove.Should().BeTrue();
        rule.IsEnabled.Should().BeTrue();
        rule.UseCount.Should().Be(0);
        rule.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SaveRuleAsync_ExistingRule_UpdatesRule()
    {
        // Arrange
        await _ruleService.SaveRuleAsync(".pdf", @"C:\OldPath", autoMove: false);

        // Act
        var updatedRule = await _ruleService.SaveRuleAsync(".pdf", @"C:\NewPath", autoMove: true);

        // Assert
        updatedRule.Destination.Should().Be(@"C:\NewPath");
        updatedRule.AutoMove.Should().BeTrue();
        
        var rules = await _ruleService.GetAllRulesAsync();
        rules.Should().HaveCount(1, "Should update, not create duplicate");
    }

    [Fact]
    public async Task SaveRuleAsync_NormalizesExtension()
    {
        // Act
        var rule = await _ruleService.SaveRuleAsync("PDF", @"C:\Docs");

        // Assert
        rule.Extension.Should().Be(".pdf");
    }

    [Fact]
    public async Task SaveRuleAsync_NullExtension_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _ruleService.SaveRuleAsync(null!, @"C:\Docs");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveRuleAsync_EmptyExtension_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _ruleService.SaveRuleAsync("", @"C:\Docs");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveRuleAsync_PersistsToStorage()
    {
        // Arrange
        await _ruleService.SaveRuleAsync(".png", @"C:\Pictures", autoMove: true);

        // Act - Create new service instance to test persistence
        using var newService = new RuleService(_storageService, NullLogger<RuleService>.Instance);
        var rules = await newService.GetAllRulesAsync();

        // Assert
        rules.Should().ContainSingle();
        rules[0].Extension.Should().Be(".png");
        rules[0].Destination.Should().Be(@"C:\Pictures");
        rules[0].AutoMove.Should().BeTrue();
    }

    #endregion

    #region UpdateRuleAsync

    [Fact]
    public async Task UpdateRuleAsync_ExistingRule_UpdatesAllProperties()
    {
        // Arrange
        var rule = await _ruleService.SaveRuleAsync(".pdf", @"C:\Old");
        rule.Destination = @"C:\New";
        rule.AutoMove = true;
        rule.IsEnabled = false;

        // Act
        await _ruleService.UpdateRuleAsync(rule);

        // Assert
        var updated = (await _ruleService.GetAllRulesAsync()).First(r => r.Extension == ".pdf");
        updated.Destination.Should().Be(@"C:\New");
        updated.AutoMove.Should().BeTrue();
        updated.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateRuleAsync_NonExistentRule_ThrowsInvalidOperationException()
    {
        // Arrange
        var fakeRule = new FileRule { Extension = ".fake", Destination = @"C:\Fake" };

        // Act & Assert
        var act = async () => await _ruleService.UpdateRuleAsync(fakeRule);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task UpdateRuleAsync_NullRule_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _ruleService.UpdateRuleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region RemoveRuleAsync

    [Fact]
    public async Task RemoveRuleAsync_ExistingRule_RemovesAndReturnsTrue()
    {
        // Arrange
        await _ruleService.SaveRuleAsync(".pdf", @"C:\Docs");

        // Act
        var result = await _ruleService.RemoveRuleAsync(".pdf");

        // Assert
        result.Should().BeTrue();
        (await _ruleService.GetAllRulesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveRuleAsync_NonExistentRule_ReturnsFalse()
    {
        // Act
        var result = await _ruleService.RemoveRuleAsync(".nonexistent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveRuleAsync_CaseInsensitive()
    {
        // Arrange
        await _ruleService.SaveRuleAsync(".pdf", @"C:\Docs");

        // Act
        var result = await _ruleService.RemoveRuleAsync(".PDF");

        // Assert
        result.Should().BeTrue();
        (await _ruleService.GetAllRulesAsync()).Should().BeEmpty();
    }

    #endregion

    #region UpdateRuleUsageAsync

    [Fact]
    public async Task UpdateRuleUsageAsync_ExistingRule_IncrementsUseCount()
    {
        // Arrange
        await _ruleService.SaveRuleAsync(".pdf", @"C:\Docs");

        // Act
        await _ruleService.UpdateRuleUsageAsync(".pdf");
        await _ruleService.UpdateRuleUsageAsync(".pdf");
        await _ruleService.UpdateRuleUsageAsync(".pdf");

        // Assert
        var rules = await _ruleService.GetAllRulesAsync();
        rules[0].UseCount.Should().Be(3);
    }

    [Fact]
    public async Task UpdateRuleUsageAsync_ExistingRule_UpdatesLastUsedAt()
    {
        // Arrange
        await _ruleService.SaveRuleAsync(".pdf", @"C:\Docs");
        var beforeUpdate = DateTime.UtcNow;

        // Act
        await Task.Delay(10); // Small delay to ensure timestamp difference
        await _ruleService.UpdateRuleUsageAsync(".pdf");

        // Assert
        var rules = await _ruleService.GetAllRulesAsync();
        rules[0].LastUsedAt.Should().NotBeNull();
        rules[0].LastUsedAt.Should().BeAfter(beforeUpdate);
    }

    [Fact]
    public async Task UpdateRuleUsageAsync_NonExistentRule_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        var act = async () => await _ruleService.UpdateRuleUsageAsync(".nonexistent");
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region SetAutoMoveAsync

    [Fact]
    public async Task SetAutoMoveAsync_ExistingRule_UpdatesAutoMove()
    {
        // Arrange
        await _ruleService.SaveRuleAsync(".pdf", @"C:\Docs", autoMove: false);

        // Act
        await _ruleService.SetAutoMoveAsync(".pdf", true);

        // Assert
        var rules = await _ruleService.GetAllRulesAsync();
        rules[0].AutoMove.Should().BeTrue();
    }

    [Fact]
    public async Task SetAutoMoveAsync_NonExistentRule_DoesNotThrow()
    {
        // Act & Assert - Should not throw, just no-op
        var act = async () => await _ruleService.SetAutoMoveAsync(".fake", true);
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region SetRuleEnabledAsync

    [Fact]
    public async Task SetRuleEnabledAsync_ExistingRule_UpdatesEnabled()
    {
        // Arrange
        await _ruleService.SaveRuleAsync(".pdf", @"C:\Docs");

        // Act
        await _ruleService.SetRuleEnabledAsync(".pdf", false);

        // Assert
        var rules = await _ruleService.GetAllRulesAsync();
        rules[0].IsEnabled.Should().BeFalse();
    }

    #endregion

    #region UpdateRulesDestinationAsync

    [Fact]
    public async Task UpdateRulesDestinationAsync_ExactMatch_UpdatesRule()
    {
        // Arrange
        await _ruleService.SaveRuleAsync(".pdf", @"C:\OldFolder");
        await _ruleService.SaveRuleAsync(".jpg", @"C:\Pictures");

        // Act
        var count = await _ruleService.UpdateRulesDestinationAsync(@"C:\OldFolder", @"C:\NewFolder");

        // Assert
        count.Should().Be(1);
        var rules = await _ruleService.GetAllRulesAsync();
        rules.First(r => r.Extension == ".pdf").Destination.Should().Be(@"C:\NewFolder");
        rules.First(r => r.Extension == ".jpg").Destination.Should().Be(@"C:\Pictures");
    }

    [Fact]
    public async Task UpdateRulesDestinationAsync_SubfolderMatch_UpdatesAllSubfolders()
    {
        // Arrange
        await _ruleService.SaveRuleAsync(".pdf", @"C:\Root\Subfolder1");
        await _ruleService.SaveRuleAsync(".jpg", @"C:\Root\Subfolder2");
        await _ruleService.SaveRuleAsync(".mp4", @"C:\Other");

        // Act
        var count = await _ruleService.UpdateRulesDestinationAsync(@"C:\Root", @"D:\NewRoot");

        // Assert
        count.Should().Be(2);
        var rules = await _ruleService.GetAllRulesAsync();
        rules.First(r => r.Extension == ".pdf").Destination.Should().Be(@"D:\NewRoot\Subfolder1");
        rules.First(r => r.Extension == ".jpg").Destination.Should().Be(@"D:\NewRoot\Subfolder2");
        rules.First(r => r.Extension == ".mp4").Destination.Should().Be(@"C:\Other");
    }

    [Fact]
    public async Task UpdateRulesDestinationAsync_NoMatches_ReturnsZero()
    {
        // Arrange
        await _ruleService.SaveRuleAsync(".pdf", @"C:\Documents");

        // Act
        var count = await _ruleService.UpdateRulesDestinationAsync(@"D:\Nonexistent", @"E:\New");

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task UpdateRulesDestinationAsync_EmptyOldPath_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _ruleService.UpdateRulesDestinationAsync("", @"C:\New");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region Thread Safety

    [Fact]
    public async Task ConcurrentOperations_ThreadSafe()
    {
        // Arrange
        var extensions = Enumerable.Range(1, 50).Select(i => $".ext{i}").ToList();

        // Act - Run concurrent saves
        var saveTasks = extensions.Select(ext => 
            _ruleService.SaveRuleAsync(ext, $@"C:\Folder{ext}"));
        await Task.WhenAll(saveTasks);

        // Assert
        var rules = await _ruleService.GetAllRulesAsync();
        rules.Should().HaveCount(50);
    }

    [Fact]
    public async Task ConcurrentUsageUpdates_AccumulatesCorrectly()
    {
        // Arrange
        await _ruleService.SaveRuleAsync(".test", @"C:\Test");
        const int updateCount = 100;

        // Act - Run concurrent usage updates
        var tasks = Enumerable.Range(0, updateCount)
            .Select(_ => _ruleService.UpdateRuleUsageAsync(".test"));
        await Task.WhenAll(tasks);

        // Assert
        var rules = await _ruleService.GetAllRulesAsync();
        rules[0].UseCount.Should().Be(updateCount);
    }

    #endregion

    #region Cancellation

    [Fact]
    public async Task SaveRuleAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = async () => await _ruleService.SaveRuleAsync(".pdf", @"C:\Docs", cancellationToken: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion
}

/// <summary>
/// Test implementation of IStorageService that uses a temporary directory.
/// </summary>
internal sealed class TestStorageService : IStorageService
{
    private readonly string _testDirectory;
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };

    public TestStorageService(string testDirectory)
    {
        _testDirectory = testDirectory;
        Directory.CreateDirectory(_testDirectory);
    }

    public string AppDataFolder => _testDirectory;
    public string RulesFilePath => Path.Combine(_testDirectory, "rules.json");
    public string SettingsFilePath => Path.Combine(_testDirectory, "settings.json");
    public string LogsFolder => Path.Combine(_testDirectory, "Logs");

    public void EnsureAppDataFolderExists()
    {
        if (!Directory.Exists(_testDirectory))
            Directory.CreateDirectory(_testDirectory);
    }

    public async Task<T?> ReadJsonAsync<T>(string filePath, CancellationToken cancellationToken = default) where T : class
    {
        if (!File.Exists(filePath))
            return null;

        await using var stream = File.OpenRead(filePath);
        return await System.Text.Json.JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }

    public async Task WriteJsonAsync<T>(string filePath, T data, CancellationToken cancellationToken = default) where T : class
    {
        EnsureAppDataFolderExists();
        await using var stream = File.Create(filePath);
        await System.Text.Json.JsonSerializer.SerializeAsync(stream, data, JsonOptions, cancellationToken);
    }
}
