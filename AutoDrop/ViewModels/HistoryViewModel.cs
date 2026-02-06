using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using AutoDrop.Models;
using AutoDrop.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AutoDrop.ViewModels;

/// <summary>
/// ViewModel for the Operation History window.
/// Displays all file operations with full undo capability.
/// Every move is reversible - zero risk.
/// </summary>
public partial class HistoryViewModel : Base.ViewModelBase
{
    private readonly IHistoryService _historyService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<HistoryViewModel> _logger;

    #region Observable Properties

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(CanUndoSelected))]
    [NotifyCanExecuteChangedFor(nameof(UndoSelectedCommand))]
    private OperationHistoryItem? _selectedItem;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusFilter = "All";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _undoableCount;

    [ObservableProperty]
    private int _selectedCount;

    #endregion

    #region Collections

    public ObservableCollection<OperationHistoryItem> Items { get; } = [];
    public ICollectionView FilteredItems { get; }
    
    public ObservableCollection<OperationHistoryItem> SelectedItems { get; } = [];

    public static IReadOnlyList<string> StatusFilters { get; } = ["All", "Success", "Undone", "Failed", "Undoable"];

    #endregion

    #region Computed Properties

    public bool HasSelection => SelectedItem != null || SelectedItems.Count > 0;
    
    public bool CanUndoSelected => SelectedItem?.CanUndo == true || SelectedItems.Any(i => i.CanUndo);

    #endregion

    public HistoryViewModel(
        IHistoryService historyService,
        INotificationService notificationService,
        ILogger<HistoryViewModel> logger)
    {
        _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Setup filtered collection view
        FilteredItems = CollectionViewSource.GetDefaultView(Items);
        FilteredItems.Filter = FilterItems;
        
        // Subscribe to history changes
        _historyService.HistoryChanged += OnHistoryChanged;
        
        _logger.LogDebug("HistoryViewModel initialized");
    }

    #region Filter Logic

    partial void OnSearchTextChanged(string value)
    {
        FilteredItems.Refresh();
    }

    partial void OnStatusFilterChanged(string value)
    {
        FilteredItems.Refresh();
    }

    private bool FilterItems(object obj)
    {
        if (obj is not OperationHistoryItem item) return false;

        // Status filter
        var passesStatusFilter = StatusFilter switch
        {
            "Success" => item.Status == OperationStatus.Success,
            "Undone" => item.Status == OperationStatus.Undone,
            "Failed" => item.Status == OperationStatus.Failed,
            "Undoable" => item.CanUndo,
            _ => true // "All"
        };

        if (!passesStatusFilter) return false;

        // Search filter
        if (string.IsNullOrWhiteSpace(SearchText)) return true;

        var search = SearchText.Trim();
        return item.ItemName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               item.SourcePath.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               item.DestinationPath.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Commands

    /// <summary>
    /// Loads operation history.
    /// </summary>
    [RelayCommand]
    public async Task LoadHistoryAsync()
    {
        if (IsLoading) return;
        
        IsLoading = true;
        try
        {
            var items = await _historyService.GetAllAsync();
            
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }

            UpdateCounts();
            FilteredItems.Refresh();
            
            _logger.LogDebug("Loaded {Count} history items", items.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load history");
            _notificationService.ShowError("Failed to load history", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Refreshes history from disk.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await _historyService.RefreshAsync();
        await LoadHistoryAsync();
    }

    /// <summary>
    /// Undoes the selected operation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUndoSelected))]
    private async Task UndoSelectedAsync()
    {
        if (SelectedItem == null && SelectedItems.Count == 0) return;

        var itemsToUndo = SelectedItems.Count > 0 
            ? SelectedItems.Where(i => i.CanUndo).ToList() 
            : SelectedItem?.CanUndo == true 
                ? [SelectedItem] 
                : [];

        if (itemsToUndo.Count == 0)
        {
            _notificationService.ShowWarning("Cannot Undo", "Selected operation(s) cannot be undone.");
            return;
        }

        IsLoading = true;
        try
        {
            var (succeeded, failed) = await _historyService.UndoMultipleAsync(
                itemsToUndo.Select(i => i.Id));

            await LoadHistoryAsync();

            if (failed == 0)
            {
                _notificationService.ShowSuccess(
                    "Undo Complete", 
                    $"Successfully undone {succeeded} operation(s).");
            }
            else
            {
                _notificationService.ShowWarning(
                    "Partial Undo", 
                    $"Undone {succeeded}, failed {failed} operation(s).");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to undo operations");
            _notificationService.ShowError("Undo Failed", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Undoes a specific operation.
    /// </summary>
    [RelayCommand]
    private async Task UndoOperationAsync(OperationHistoryItem? item)
    {
        if (item == null || !item.CanUndo) return;

        IsLoading = true;
        try
        {
            var success = await _historyService.UndoOperationAsync(item.Id);
            await LoadHistoryAsync();

            if (success)
            {
                _notificationService.ShowSuccess("Undone", $"Moved {item.ItemName} back to original location.");
            }
            else
            {
                _notificationService.ShowError("Undo Failed", "Could not restore the file to its original location.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to undo operation: {ItemName}", item.ItemName);
            _notificationService.ShowError("Undo Failed", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Clears all history.
    /// </summary>
    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        if (Items.Count == 0) return;

        // In a real app, we'd show a confirmation dialog first
        IsLoading = true;
        try
        {
            await _historyService.ClearHistoryAsync();
            Items.Clear();
            UpdateCounts();
            
            _notificationService.ShowSuccess("History Cleared", "All operation history has been removed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear history");
            _notificationService.ShowError("Clear Failed", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Opens the source folder in Explorer.
    /// </summary>
    [RelayCommand]
    private void OpenSourceFolder(OperationHistoryItem? item)
    {
        if (item == null) return;

        var folder = Path.GetDirectoryName(item.SourcePath);
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
        {
            System.Diagnostics.Process.Start("explorer.exe", folder);
        }
    }

    /// <summary>
    /// Opens the destination folder in Explorer.
    /// </summary>
    [RelayCommand]
    private void OpenDestinationFolder(OperationHistoryItem? item)
    {
        if (item == null) return;

        var folder = Path.GetDirectoryName(item.DestinationPath);
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
        {
            System.Diagnostics.Process.Start("explorer.exe", folder);
        }
        else if (File.Exists(item.DestinationPath))
        {
            // Select the file in Explorer
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{item.DestinationPath}\"");
        }
    }

    #endregion

    #region Helper Methods

    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        // Re-load on change (could be from another part of the app)
        System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            await LoadHistoryAsync();
        });
    }

    private void UpdateCounts()
    {
        TotalCount = Items.Count;
        UndoableCount = Items.Count(i => i.CanUndo);
        SelectedCount = SelectedItems.Count;
    }

    public void UpdateSelection(IList<object> selectedItems)
    {
        SelectedItems.Clear();
        foreach (var item in selectedItems.OfType<OperationHistoryItem>())
        {
            SelectedItems.Add(item);
        }
        SelectedCount = SelectedItems.Count;
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanUndoSelected));
        UndoSelectedCommand.NotifyCanExecuteChanged();
    }

    #endregion
}
