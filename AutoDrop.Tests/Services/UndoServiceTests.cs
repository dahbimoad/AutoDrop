using Microsoft.Extensions.Logging.Abstractions;

namespace AutoDrop.Tests.Services;

/// <summary>
/// Unit tests for UndoService.
/// Tests batch undo operations, expiration, thread safety, and event handling.
/// </summary>
public sealed class UndoServiceTests : IDisposable
{
    private readonly UndoService _undoService;
    private readonly List<UndoAvailableEventArgs> _undoAvailableEvents = [];
    private readonly List<UndoExecutedEventArgs> _undoExecutedEvents = [];

    public UndoServiceTests()
    {
        _undoService = new UndoService(NullLogger<UndoService>.Instance);
        _undoService.UndoAvailable += (_, e) => _undoAvailableEvents.Add(e);
        _undoService.UndoExecuted += (_, e) => _undoExecutedEvents.Add(e);
    }

    public void Dispose()
    {
        _undoService.Dispose();
    }

    #region Initial State

    [Fact]
    public void InitialState_CanUndo_IsFalse()
    {
        // Assert
        _undoService.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void InitialState_PendingOperationsCount_IsZero()
    {
        // Assert
        _undoService.PendingOperationsCount.Should().Be(0);
    }

    [Fact]
    public void InitialState_CurrentOperationDescription_IsNull()
    {
        // Assert
        _undoService.CurrentOperationDescription.Should().BeNull();
    }

    #endregion

    #region RegisterOperation

    [Fact]
    public void RegisterOperation_SingleOperation_SetsCanUndo()
    {
        // Act
        _undoService.RegisterOperation("Test", () => Task.FromResult(true));

        // Assert
        _undoService.CanUndo.Should().BeTrue();
        _undoService.PendingOperationsCount.Should().Be(1);
    }

    [Fact]
    public void RegisterOperation_SingleOperation_SetsDescription()
    {
        // Act
        _undoService.RegisterOperation("Move file.txt", () => Task.FromResult(true));

        // Assert
        _undoService.CurrentOperationDescription.Should().Be("Move file.txt");
    }

    [Fact]
    public void RegisterOperation_MultipleOperations_UpdatesCount()
    {
        // Act
        _undoService.RegisterOperation("Op1", () => Task.FromResult(true));
        _undoService.RegisterOperation("Op2", () => Task.FromResult(true));
        _undoService.RegisterOperation("Op3", () => Task.FromResult(true));

        // Assert
        _undoService.PendingOperationsCount.Should().Be(3);
        _undoService.CurrentOperationDescription.Should().Be("3 items");
    }

    [Fact]
    public void RegisterOperation_RaisesUndoAvailableEvent()
    {
        // Act
        _undoService.RegisterOperation("Test operation", () => Task.FromResult(true), 5);

        // Assert
        _undoAvailableEvents.Should().ContainSingle();
        _undoAvailableEvents[0].Description.Should().Be("Test operation");
        _undoAvailableEvents[0].ExpirationSeconds.Should().Be(5);
        _undoAvailableEvents[0].TotalCount.Should().Be(1);
    }

    [Fact]
    public void RegisterOperation_MultipleOperations_EventShowsCorrectCount()
    {
        // Act
        _undoService.RegisterOperation("Op1", () => Task.FromResult(true));
        _undoService.RegisterOperation("Op2", () => Task.FromResult(true));

        // Assert
        _undoAvailableEvents.Should().HaveCount(2);
        _undoAvailableEvents[1].TotalCount.Should().Be(2);
        _undoAvailableEvents[1].Description.Should().Be("2 items");
    }

    [Fact]
    public void RegisterOperation_NullDescription_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _undoService.RegisterOperation(null!, () => Task.FromResult(true));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterOperation_EmptyDescription_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _undoService.RegisterOperation("   ", () => Task.FromResult(true));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterOperation_NullUndoAction_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _undoService.RegisterOperation("Test", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegisterOperation_ZeroExpiration_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var act = () => _undoService.RegisterOperation("Test", () => Task.FromResult(true), 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RegisterOperation_NegativeExpiration_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var act = () => _undoService.RegisterOperation("Test", () => Task.FromResult(true), -5);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region ExecuteUndoAsync

    [Fact]
    public async Task ExecuteUndoAsync_NoPendingOperations_ReturnsFalse()
    {
        // Act
        var result = await _undoService.ExecuteUndoAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteUndoAsync_SingleOperation_ExecutesAndReturnsTrue()
    {
        // Arrange
        var executed = false;
        _undoService.RegisterOperation("Test", () =>
        {
            executed = true;
            return Task.FromResult(true);
        });

        // Act
        var result = await _undoService.ExecuteUndoAsync();

        // Assert
        result.Should().BeTrue();
        executed.Should().BeTrue();
        _undoService.CanUndo.Should().BeFalse();
        _undoService.PendingOperationsCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteUndoAsync_MultipleOperations_ExecutesAllInReverseOrder()
    {
        // Arrange
        var executionOrder = new List<string>();
        _undoService.RegisterOperation("First", () =>
        {
            executionOrder.Add("First");
            return Task.FromResult(true);
        });
        _undoService.RegisterOperation("Second", () =>
        {
            executionOrder.Add("Second");
            return Task.FromResult(true);
        });
        _undoService.RegisterOperation("Third", () =>
        {
            executionOrder.Add("Third");
            return Task.FromResult(true);
        });

        // Act
        await _undoService.ExecuteUndoAsync();

        // Assert - LIFO order (last in, first out)
        executionOrder.Should().Equal("Third", "Second", "First");
    }

    [Fact]
    public async Task ExecuteUndoAsync_OperationReturnsFalse_ReportsFailed()
    {
        // Arrange
        _undoService.RegisterOperation("Failing", () => Task.FromResult(false));

        // Act
        var result = await _undoService.ExecuteUndoAsync();

        // Assert
        result.Should().BeFalse();
        _undoExecutedEvents.Should().ContainSingle();
        _undoExecutedEvents[0].Success.Should().BeFalse();
        _undoExecutedEvents[0].FailedCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteUndoAsync_OperationThrows_ReportsFailedAndContinues()
    {
        // Arrange
        var secondExecuted = false;
        _undoService.RegisterOperation("Throwing", () => throw new InvalidOperationException("Test error"));
        _undoService.RegisterOperation("Normal", () =>
        {
            secondExecuted = true;
            return Task.FromResult(true);
        });

        // Act
        var result = await _undoService.ExecuteUndoAsync();

        // Assert
        result.Should().BeFalse("One operation failed");
        secondExecuted.Should().BeTrue("Should continue after exception");
        _undoExecutedEvents[0].UndoneCount.Should().Be(1);
        _undoExecutedEvents[0].FailedCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteUndoAsync_MixedResults_ReportsCorrectCounts()
    {
        // Arrange
        _undoService.RegisterOperation("Success1", () => Task.FromResult(true));
        _undoService.RegisterOperation("Fail1", () => Task.FromResult(false));
        _undoService.RegisterOperation("Success2", () => Task.FromResult(true));
        _undoService.RegisterOperation("Throw1", () => throw new Exception());

        // Act
        await _undoService.ExecuteUndoAsync();

        // Assert
        _undoExecutedEvents[0].UndoneCount.Should().Be(2);
        _undoExecutedEvents[0].FailedCount.Should().Be(2);
        _undoExecutedEvents[0].ErrorMessage.Should().Contain("Failed to undo 2 item(s)");
    }

    [Fact]
    public async Task ExecuteUndoAsync_RaisesUndoExecutedEvent()
    {
        // Arrange
        _undoService.RegisterOperation("Test op", () => Task.FromResult(true));

        // Act
        await _undoService.ExecuteUndoAsync();

        // Assert
        _undoExecutedEvents.Should().ContainSingle();
        _undoExecutedEvents[0].Success.Should().BeTrue();
        _undoExecutedEvents[0].Description.Should().Be("Test op");
        _undoExecutedEvents[0].UndoneCount.Should().Be(1);
        _undoExecutedEvents[0].FailedCount.Should().Be(0);
        _undoExecutedEvents[0].ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteUndoAsync_ClearsOperationsAfterExecution()
    {
        // Arrange
        _undoService.RegisterOperation("Test", () => Task.FromResult(true));

        // Act
        await _undoService.ExecuteUndoAsync();

        // Assert
        _undoService.CanUndo.Should().BeFalse();
        _undoService.PendingOperationsCount.Should().Be(0);
        _undoService.CurrentOperationDescription.Should().BeNull();
    }

    #endregion

    #region ClearUndo

    [Fact]
    public void ClearUndo_WithPendingOperations_ClearsAll()
    {
        // Arrange
        _undoService.RegisterOperation("Op1", () => Task.FromResult(true));
        _undoService.RegisterOperation("Op2", () => Task.FromResult(true));

        // Act
        _undoService.ClearUndo();

        // Assert
        _undoService.CanUndo.Should().BeFalse();
        _undoService.PendingOperationsCount.Should().Be(0);
    }

    [Fact]
    public void ClearUndo_NoPendingOperations_DoesNotThrow()
    {
        // Act & Assert
        var act = () => _undoService.ClearUndo();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task ClearUndo_OperationsNotExecutedAfterClear()
    {
        // Arrange
        var executed = false;
        _undoService.RegisterOperation("Test", () =>
        {
            executed = true;
            return Task.FromResult(true);
        });

        // Act
        _undoService.ClearUndo();
        await _undoService.ExecuteUndoAsync();

        // Assert
        executed.Should().BeFalse("Operation should not execute after clear");
    }

    #endregion

    #region Expiration

    [Fact]
    public async Task Expiration_AfterTimeout_ClearsOperations()
    {
        // Arrange - Use short expiration for test
        _undoService.RegisterOperation("Expiring", () => Task.FromResult(true), expirationSeconds: 1);
        _undoService.CanUndo.Should().BeTrue();

        // Act - Wait for expiration
        await Task.Delay(1500);

        // Assert
        _undoService.CanUndo.Should().BeFalse();
        _undoService.PendingOperationsCount.Should().Be(0);
    }

    [Fact]
    public async Task Expiration_NewOperationResetsTimer()
    {
        // Arrange
        _undoService.RegisterOperation("First", () => Task.FromResult(true), expirationSeconds: 1);
        
        // Wait partial time
        await Task.Delay(700);
        
        // Add new operation (should reset timer)
        _undoService.RegisterOperation("Second", () => Task.FromResult(true), expirationSeconds: 1);

        // Wait time that would have expired first operation
        await Task.Delay(500);

        // Assert - Both should still be available
        _undoService.CanUndo.Should().BeTrue();
        _undoService.PendingOperationsCount.Should().Be(2);
    }

    #endregion

    #region Thread Safety

    [Fact]
    public void ConcurrentRegistrations_AllOperationsRegistered()
    {
        // Arrange
        const int operationCount = 100;

        // Act
        Parallel.For(0, operationCount, i =>
        {
            _undoService.RegisterOperation($"Op{i}", () => Task.FromResult(true));
        });

        // Assert
        _undoService.PendingOperationsCount.Should().Be(operationCount);
    }

    [Fact]
    public async Task ConcurrentExecuteAndRegister_HandlesGracefully()
    {
        // Arrange
        const int iterations = 50;
        var exceptions = new List<Exception>();

        // Act
        var tasks = new List<Task>();
        
        for (int i = 0; i < iterations; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    _undoService.RegisterOperation("Concurrent", () => Task.FromResult(true));
                }
                catch (Exception ex)
                {
                    lock (exceptions) exceptions.Add(ex);
                }
            }));

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await _undoService.ExecuteUndoAsync();
                }
                catch (Exception ex)
                {
                    lock (exceptions) exceptions.Add(ex);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - No exceptions should occur
        exceptions.Should().BeEmpty();
    }

    #endregion

    #region Dispose

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        // Act & Assert
        var act = () =>
        {
            _undoService.Dispose();
            _undoService.Dispose();
            _undoService.Dispose();
        };
        act.Should().NotThrow();
    }

    #endregion
}
