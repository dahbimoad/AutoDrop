using System.Timers;
using AutoDrop.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

namespace AutoDrop.Services.Implementations;

/// <summary>
/// Implementation of the undo service.
/// Manages multiple undoable operations with automatic expiration.
/// Supports batch undo for multiple file operations.
/// Thread-safe and follows SOLID principles.
/// </summary>
public sealed class UndoService : IUndoService, IDisposable
{
    private readonly ILogger<UndoService> _logger;
    private readonly object _lock = new();
    private readonly Timer _expirationTimer;
    private readonly List<UndoOperation> _pendingOperations = [];

    private bool _disposed;

    public event EventHandler<UndoAvailableEventArgs>? UndoAvailable;
    public event EventHandler<UndoExecutedEventArgs>? UndoExecuted;

    public bool CanUndo
    {
        get
        {
            lock (_lock)
            {
                return _pendingOperations.Count > 0;
            }
        }
    }

    public string? CurrentOperationDescription
    {
        get
        {
            lock (_lock)
            {
                if (_pendingOperations.Count == 0) return null;
                if (_pendingOperations.Count == 1) return _pendingOperations[0].Description;
                return $"{_pendingOperations.Count} items";
            }
        }
    }

    public int PendingOperationsCount
    {
        get
        {
            lock (_lock)
            {
                return _pendingOperations.Count;
            }
        }
    }

    public UndoService(ILogger<UndoService> logger)
    {
        _logger = logger;
        _expirationTimer = new Timer { AutoReset = false };
        _expirationTimer.Elapsed += OnExpirationTimerElapsed;
        
        _logger.LogDebug("UndoService initialized");
    }

    /// <inheritdoc />
    public void RegisterOperation(string description, Func<Task<bool>> undoAction, int expirationSeconds = 10)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(undoAction);

        if (expirationSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(expirationSeconds), "Expiration must be positive.");

        int totalCount;
        lock (_lock)
        {
            // Reset timer on each new operation (extends the batch window)
            _expirationTimer.Stop();

            // Add to pending operations
            _pendingOperations.Add(new UndoOperation(description, undoAction));
            totalCount = _pendingOperations.Count;

            // Start/restart expiration timer
            _expirationTimer.Interval = expirationSeconds * 1000;
            _expirationTimer.Start();

            _logger.LogDebug("Undo registered: {Description}, total pending: {Count}", description, totalCount);
        }

        // Raise event outside lock to prevent deadlocks
        var displayDescription = totalCount == 1 ? description : $"{totalCount} items";
        UndoAvailable?.Invoke(this, new UndoAvailableEventArgs
        {
            Description = displayDescription,
            ExpirationSeconds = expirationSeconds,
            TotalCount = totalCount
        });
    }

    /// <inheritdoc />
    public async Task<bool> ExecuteUndoAsync()
    {
        List<UndoOperation> operationsToUndo;
        
        lock (_lock)
        {
            if (_pendingOperations.Count == 0)
            {
                _logger.LogWarning("ExecuteUndo called but no undo operations available");
                return false;
            }

            _expirationTimer.Stop();
            
            // Take all operations (reverse order - LIFO for proper undo)
            operationsToUndo = _pendingOperations.ToList();
            operationsToUndo.Reverse();
            _pendingOperations.Clear();
        }

        _logger.LogInformation("Executing undo for {Count} operations", operationsToUndo.Count);

        var undoneCount = 0;
        var failedCount = 0;
        var descriptions = new List<string>();

        foreach (var operation in operationsToUndo)
        {
            try
            {
                var success = await operation.UndoAction();
                if (success)
                {
                    undoneCount++;
                    descriptions.Add(operation.Description);
                    _logger.LogDebug("Undo successful: {Description}", operation.Description);
                }
                else
                {
                    failedCount++;
                    _logger.LogWarning("Undo returned false: {Description}", operation.Description);
                }
            }
            catch (Exception ex)
            {
                failedCount++;
                _logger.LogError(ex, "Undo failed: {Description}", operation.Description);
            }
        }

        var resultDescription = undoneCount == 1 
            ? descriptions.FirstOrDefault() ?? "item" 
            : $"{undoneCount} items";

        UndoExecuted?.Invoke(this, new UndoExecutedEventArgs
        {
            Success = failedCount == 0,
            Description = resultDescription,
            UndoneCount = undoneCount,
            FailedCount = failedCount,
            ErrorMessage = failedCount > 0 ? $"Failed to undo {failedCount} item(s)" : null
        });

        _logger.LogInformation("Undo completed: {Undone} succeeded, {Failed} failed", undoneCount, failedCount);
        return failedCount == 0;
    }

    /// <inheritdoc />
    public void ClearUndo()
    {
        lock (_lock)
        {
            _expirationTimer.Stop();
            _pendingOperations.Clear();
            _logger.LogDebug("All undo operations cleared");
        }
    }

    private void OnExpirationTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        lock (_lock)
        {
            if (_pendingOperations.Count > 0)
            {
                _logger.LogDebug("Undo expired: {Count} operations cleared", _pendingOperations.Count);
                _pendingOperations.Clear();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _expirationTimer.Stop();
        _expirationTimer.Elapsed -= OnExpirationTimerElapsed;
        _expirationTimer.Dispose();
        _disposed = true;
        
        _logger.LogDebug("UndoService disposed");
    }

    /// <summary>
    /// Internal record to hold undo operation data.
    /// </summary>
    private sealed record UndoOperation(string Description, Func<Task<bool>> UndoAction);
}
