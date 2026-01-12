using System.Collections.ObjectModel;
using AutoDrop.Models;
using AutoDrop.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AutoDrop.ViewModels;

/// <summary>
/// ViewModel for the batch operations popup/dialog.
/// </summary>
public partial class BatchOperationViewModel : Base.ViewModelBase, IDisposable
{
    private readonly IBatchOperationService _batchService;
    private readonly INotificationService _notificationService;
    private readonly IUndoService _undoService;
    private readonly IFileOperationService _fileOperationService;
    private readonly ILogger<BatchOperationViewModel> _logger;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private string _progressStatus = string.Empty;

    [ObservableProperty]
    private string _currentItem = string.Empty;

    [ObservableProperty]
    private int _totalFiles;

    [ObservableProperty]
    private int _totalGroups;

    [ObservableProperty]
    private DuplicateHandling _selectedDuplicateHandling = DuplicateHandling.KeepBothAll;

    /// <summary>
    /// Groups of files organized by destination.
    /// </summary>
    public ObservableCollection<BatchFileGroupViewModel> Groups { get; } = [];

    /// <summary>
    /// Available duplicate handling options.
    /// </summary>
    public IReadOnlyList<DuplicateHandlingOption> DuplicateHandlingOptions { get; } =
    [
        new("Keep both (auto-rename)", DuplicateHandling.KeepBothAll),
        new("Skip duplicates", DuplicateHandling.SkipAll),
        new("Replace existing", DuplicateHandling.ReplaceAll),
        new("Delete source if identical", DuplicateHandling.DeleteSourceAll)
    ];

    /// <summary>
    /// Event raised when the operation is complete.
    /// </summary>
    public event EventHandler<BatchOperationCompletedEventArgs>? OperationCompleted;

    /// <summary>
    /// Event raised to request closing the dialog.
    /// </summary>
    public event EventHandler? CloseRequested;

    public BatchOperationViewModel(
        IBatchOperationService batchService,
        INotificationService notificationService,
        IUndoService undoService,
        IFileOperationService fileOperationService,
        ILogger<BatchOperationViewModel> logger)
    {
        _batchService = batchService ?? throw new ArgumentNullException(nameof(batchService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _undoService = undoService ?? throw new ArgumentNullException(nameof(undoService));
        _fileOperationService = fileOperationService ?? throw new ArgumentNullException(nameof(fileOperationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("BatchOperationViewModel initialized");
    }

    /// <summary>
    /// Synchronous initialization that fires and forgets the async operation.
    /// </summary>
    /// <param name="items">The dropped items to process.</param>
    public void Initialize(IEnumerable<DroppedItem> items)
    {
        // Fire and forget with proper error handling
        _ = InitializeAsync(items).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                _logger.LogError(t.Exception, "Failed to initialize batch operation");
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    _notificationService.ShowError("Error", "Failed to analyze files for batch operation.");
                });
            }
        }, TaskScheduler.Default);
    }

    /// <summary>
    /// Initializes the view model with dropped items.
    /// </summary>
    public async Task InitializeAsync(IEnumerable<DroppedItem> items, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Initializing batch operation view model");

        Groups.Clear();
        IsProcessing = true;
        ProgressStatus = "Analyzing files...";

        try
        {
            var groups = await _batchService.GroupItemsByDestinationAsync(items, cancellationToken);

            foreach (var group in groups)
            {
                Groups.Add(new BatchFileGroupViewModel(group));
            }

            TotalFiles = Groups.Sum(g => g.FileCount);
            TotalGroups = Groups.Count;

            _logger.LogDebug("Initialized with {Files} files in {Groups} groups", TotalFiles, TotalGroups);
        }
        finally
        {
            IsProcessing = false;
            ProgressStatus = string.Empty;
        }
    }

    /// <summary>
    /// Executes the batch move operation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ExecuteAsync()
    {
        if (IsProcessing) return;

        var selectedGroups = Groups
            .Where(g => g.IsSelected)
            .Select(g => g.Model)
            .ToList();

        if (selectedGroups.Count == 0)
        {
            _notificationService.ShowError("No Selection", "Please select at least one group to organize.");
            return;
        }

        _logger.LogInformation("Executing batch operation for {Groups} groups", selectedGroups.Count);

        IsProcessing = true;
        _cts = new CancellationTokenSource();

        var progress = new Progress<BatchProgressReport>(report =>
        {
            ProgressPercent = report.ProgressPercent;
            ProgressStatus = report.Status;
            CurrentItem = report.CurrentItem;
        });

        try
        {
            var result = await _batchService.ExecuteBatchMoveAsync(
                selectedGroups,
                progress,
                SelectedDuplicateHandling,
                _cts.Token);

            // Register undo for the entire batch
            if (result.Operations.Count > 0)
            {
                RegisterBatchUndo(result);
            }

            // Show notification
            if (result.IsFullSuccess)
            {
                _notificationService.ShowSuccess(
                    "Batch Complete",
                    result.GetSummaryMessage());
            }
            else if (result.SuccessCount > 0)
            {
                _notificationService.ShowWarning(
                    "Batch Partially Complete",
                    result.GetSummaryMessage());
            }
            else
            {
                _notificationService.ShowError(
                    "Batch Failed",
                    result.GetSummaryMessage());
            }

            OperationCompleted?.Invoke(this, new BatchOperationCompletedEventArgs(result));
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Batch operation cancelled by user");
            _notificationService.ShowWarning("Cancelled", "Batch operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch operation failed");
            _notificationService.ShowError("Batch Failed", ex.Message);
        }
        finally
        {
            IsProcessing = false;
            ProgressPercent = 0;
            ProgressStatus = string.Empty;
            CurrentItem = string.Empty;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private bool CanExecute() => !IsProcessing && Groups.Any(g => g.IsSelected);

    /// <summary>
    /// Cancels the current operation.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        if (IsProcessing)
        {
            _logger.LogDebug("Cancelling batch operation");
            _cts?.Cancel();
        }
        else
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Toggles selection of all groups.
    /// </summary>
    [RelayCommand]
    private void ToggleAll()
    {
        var allSelected = Groups.All(g => g.IsSelected);
        foreach (var group in Groups)
        {
            group.IsSelected = !allSelected;
        }
    }

    /// <summary>
    /// Registers an undo operation for the batch.
    /// </summary>
    private void RegisterBatchUndo(BatchOperationResult result)
    {
        var operations = result.Operations;
        var description = result.SuccessCount == 1
            ? operations[0].ItemName
            : $"{result.SuccessCount} files to {result.DestinationCount} folder{(result.DestinationCount == 1 ? "" : "s")}";

        _undoService.RegisterOperation(
            description,
            async () =>
            {
                var undoneCount = await _batchService.UndoBatchAsync(operations);
                return undoneCount == operations.Count;
            },
            expirationSeconds: 15);
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _cts?.Cancel();
        _cts?.Dispose();
        _disposed = true;

        _logger.LogDebug("BatchOperationViewModel disposed");
    }
}

/// <summary>
/// ViewModel wrapper for BatchFileGroup with property change notification.
/// </summary>
public partial class BatchFileGroupViewModel : ObservableObject
{
    private readonly BatchFileGroup _model;

    [ObservableProperty]
    private bool _isSelected;

    public BatchFileGroup Model => _model;
    public string Category => _model.Category;
    public string DestinationPath => _model.DestinationPath;
    public string DestinationDisplayName => _model.DestinationDisplayName;
    public int FileCount => _model.FileCount;
    public string DisplayText => _model.DisplayText;
    public long TotalSize => _model.TotalSize;
    public IReadOnlyList<DroppedItem> Items => _model.Items;

    public BatchFileGroupViewModel(BatchFileGroup model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _isSelected = model.IsSelected;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        _model.IsSelected = value;
    }
}

/// <summary>
/// Option for duplicate handling dropdown.
/// </summary>
public sealed record DuplicateHandlingOption(string DisplayName, DuplicateHandling Value);

/// <summary>
/// Event args for batch operation completion.
/// </summary>
public sealed class BatchOperationCompletedEventArgs : EventArgs
{
    public BatchOperationResult Result { get; }

    public BatchOperationCompletedEventArgs(BatchOperationResult result)
    {
        Result = result;
    }
}
