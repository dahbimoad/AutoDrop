using AutoDrop.Models;
using AutoDrop.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoDrop.Services.Implementations;

/// <summary>
/// Implementation of the history service.
/// Tracks all file operations with full rollback capability.
/// Persists history to JSON file for cross-session access.
/// </summary>
public sealed class HistoryService : IHistoryService, IDisposable
{
    private readonly IStorageService _storageService;
    private readonly ILogger<HistoryService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    private const string HistoryFileName = "history.json";
    private const int MaxHistoryItems = 100;
    
    private OperationHistoryData _historyData = new();
    private bool _isLoaded;
    private bool _disposed;

    public event EventHandler? HistoryChanged;

    public int TotalCount => _historyData.Items.Count;
    
    public int UndoableCount => _historyData.Items.Count(i => i.CanUndo);

    public HistoryService(IStorageService storageService, ILogger<HistoryService> logger)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogDebug("HistoryService initialized");
    }

    private string HistoryFilePath => Path.Combine(_storageService.AppDataFolder, HistoryFileName);

    /// <inheritdoc />
    public async Task AddOperationAsync(OperationHistoryItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(ct).ConfigureAwait(false);
            
            // Add to beginning (newest first)
            _historyData.Items.Insert(0, item);
            
            // Trim old entries
            while (_historyData.Items.Count > MaxHistoryItems)
            {
                _historyData.Items.RemoveAt(_historyData.Items.Count - 1);
            }
            
            await SaveAsync(ct).ConfigureAwait(false);
            
            _logger.LogDebug("Operation added to history: {ItemName} -> {Destination}", 
                item.ItemName, item.DestinationPath);
        }
        finally
        {
            _lock.Release();
        }
        
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public async Task<OperationHistoryItem> RecordOperationAsync(
        string sourcePath, 
        string destinationPath, 
        OperationType operationType = OperationType.Move,
        double? aiConfidence = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var isDirectory = Directory.Exists(destinationPath);
        long sizeBytes = 0;
        
        if (!isDirectory && File.Exists(destinationPath))
        {
            try
            {
                sizeBytes = new FileInfo(destinationPath).Length;
            }
            catch
            {
                // Ignore file access errors
            }
        }

        var item = new OperationHistoryItem
        {
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            ItemName = Path.GetFileName(sourcePath),
            IsDirectory = isDirectory,
            SizeBytes = sizeBytes,
            OperationType = operationType,
            Status = OperationStatus.Success,
            AiConfidence = aiConfidence
        };

        await AddOperationAsync(item, ct).ConfigureAwait(false);
        return item;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OperationHistoryItem>> GetAllAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(ct).ConfigureAwait(false);
            return _historyData.Items.ToList().AsReadOnly();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OperationHistoryItem>> GetRecentAsync(int count = 20, CancellationToken ct = default)
    {
        if (count <= 0) count = 20;
        
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(ct).ConfigureAwait(false);
            return _historyData.Items.Take(count).ToList().AsReadOnly();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OperationHistoryItem>> GetUndoableAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(ct).ConfigureAwait(false);
            return _historyData.Items.Where(i => i.CanUndo).ToList().AsReadOnly();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> UndoOperationAsync(Guid operationId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(ct).ConfigureAwait(false);
            
            var operation = _historyData.Items.FirstOrDefault(i => i.Id == operationId);
            if (operation == null)
            {
                _logger.LogWarning("Operation not found for undo: {OperationId}", operationId);
                return false;
            }

            if (!operation.CanUndo)
            {
                _logger.LogWarning("Operation cannot be undone: {ItemName}, Status: {Status}", 
                    operation.ItemName, operation.Status);
                return false;
            }

            // Perform the undo (move back to original location)
            var success = await PerformUndoAsync(operation, ct).ConfigureAwait(false);
            
            if (success)
            {
                operation.Status = OperationStatus.Undone;
                operation.UndoneAt = DateTime.UtcNow;
                await SaveAsync(ct).ConfigureAwait(false);
                _logger.LogInformation("Undone operation: {ItemName}", operation.ItemName);
            }
            
            return success;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<(int succeeded, int failed)> UndoMultipleAsync(IEnumerable<Guid> operationIds, CancellationToken ct = default)
    {
        var ids = operationIds.ToList();
        var succeeded = 0;
        var failed = 0;

        // Process in reverse order (newest first) to handle dependencies
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            
            try
            {
                var success = await UndoOperationAsync(id, ct).ConfigureAwait(false);
                if (success) succeeded++;
                else failed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to undo operation: {OperationId}", id);
                failed++;
            }
        }

        HistoryChanged?.Invoke(this, EventArgs.Empty);
        return (succeeded, failed);
    }

    /// <inheritdoc />
    public async Task ClearHistoryAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _historyData.Items.Clear();
            await SaveAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("History cleared");
        }
        finally
        {
            _lock.Release();
        }
        
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _isLoaded = false;
            await EnsureLoadedAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
        
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public async Task MarkAsFailedAsync(Guid operationId, string errorMessage, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(ct).ConfigureAwait(false);
            
            var operation = _historyData.Items.FirstOrDefault(i => i.Id == operationId);
            if (operation != null)
            {
                operation.Status = OperationStatus.Failed;
                operation.ErrorMessage = errorMessage;
                await SaveAsync(ct).ConfigureAwait(false);
                _logger.LogWarning("Operation marked as failed: {ItemName}, Error: {Error}", 
                    operation.ItemName, errorMessage);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (_isLoaded) return;

        try
        {
            var data = await _storageService.ReadJsonAsync<OperationHistoryData>(HistoryFilePath, ct)
                .ConfigureAwait(false);
            
            _historyData = data ?? new OperationHistoryData();
            _isLoaded = true;
            _logger.LogDebug("Loaded {Count} history items", _historyData.Items.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load history, starting fresh");
            _historyData = new OperationHistoryData();
            _isLoaded = true;
        }
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await _storageService.WriteJsonAsync(HistoryFilePath, _historyData, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save history");
        }
    }

    private async Task<bool> PerformUndoAsync(OperationHistoryItem operation, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            // Check if source location is available
            var sourceDir = Path.GetDirectoryName(operation.SourcePath);
            if (!string.IsNullOrEmpty(sourceDir) && !Directory.Exists(sourceDir))
            {
                Directory.CreateDirectory(sourceDir);
            }

            // Handle destination filename conflicts
            var finalSourcePath = operation.SourcePath;
            if ((operation.IsDirectory && Directory.Exists(finalSourcePath)) ||
                (!operation.IsDirectory && File.Exists(finalSourcePath)))
            {
                // Generate unique name
                var dir = Path.GetDirectoryName(finalSourcePath) ?? string.Empty;
                var name = Path.GetFileNameWithoutExtension(finalSourcePath);
                var ext = Path.GetExtension(finalSourcePath);
                var counter = 1;
                
                while (operation.IsDirectory ? Directory.Exists(finalSourcePath) : File.Exists(finalSourcePath))
                {
                    finalSourcePath = Path.Combine(dir, $"{name} ({counter}){ext}");
                    counter++;
                }
            }

            // Move back to source
            await Task.Run(() =>
            {
                if (operation.IsDirectory)
                {
                    Directory.Move(operation.DestinationPath, finalSourcePath);
                }
                else
                {
                    File.Move(operation.DestinationPath, finalSourcePath);
                }
            }, ct).ConfigureAwait(false);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to undo operation: {ItemName}", operation.ItemName);
            operation.ErrorMessage = ex.Message;
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lock.Dispose();
        _logger.LogDebug("HistoryService disposed");
    }
}
