using System.Collections.ObjectModel;
using AutoDrop.Models;
using AutoDrop.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AutoDrop.ViewModels;

/// <summary>
/// ViewModel for the main drop zone window.
/// </summary>
public partial class DropZoneViewModel : Base.ViewModelBase
{
    private readonly IFileOperationService _fileOperationService;
    private readonly IDestinationSuggestionService _suggestionService;
    private readonly IRuleService _ruleService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<DropZoneViewModel> _logger;

    [ObservableProperty]
    private bool _isDragOver;

    [ObservableProperty]
    private bool _isPopupOpen;

    [ObservableProperty]
    private string _statusText = "Drop files here";

    [ObservableProperty]
    private DroppedItem? _currentItem;

    [ObservableProperty]
    private bool _rememberChoice;

    [ObservableProperty]
    private bool _enableAutoMove;

    [ObservableProperty]
    private MoveOperation? _lastOperation;

    public ObservableCollection<DroppedItem> DroppedItems { get; } = [];
    public ObservableCollection<DestinationSuggestion> Suggestions { get; } = [];

    public DropZoneViewModel(
        IFileOperationService fileOperationService,
        IDestinationSuggestionService suggestionService,
        IRuleService ruleService,
        INotificationService notificationService,
        ILogger<DropZoneViewModel> logger)
    {
        _fileOperationService = fileOperationService ?? throw new ArgumentNullException(nameof(fileOperationService));
        _suggestionService = suggestionService ?? throw new ArgumentNullException(nameof(suggestionService));
        _ruleService = ruleService ?? throw new ArgumentNullException(nameof(ruleService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger;
        
        _logger.LogDebug("DropZoneViewModel initialized");
    }

    /// <summary>
    /// Handles files being dropped onto the drop zone.
    /// Automatically moves files if an auto-move rule exists.
    /// </summary>
    [RelayCommand]
    private async Task HandleDropAsync(string[] paths)
    {
        if (paths is not { Length: > 0 })
            return;

        _logger.LogInformation("Files dropped: {Count} items", paths.Length);

        DroppedItems.Clear();
        Suggestions.Clear();

        foreach (var path in paths)
        {
            var item = DroppedItem.FromPath(path);
            DroppedItems.Add(item);
            _logger.LogDebug("Processing dropped item: {Name} ({Category})", item.Name, item.Category);
        }

        // Check if ALL items have auto-move rules
        var autoMoveItems = new List<(DroppedItem Item, FileRule Rule)>();
        var manualItems = new List<DroppedItem>();

        foreach (var item in DroppedItems)
        {
            if (!item.IsDirectory && !string.IsNullOrEmpty(item.Extension))
            {
                var rule = await _ruleService.GetRuleForExtensionAsync(item.Extension);
                if (rule is { AutoMove: true, IsEnabled: true } && Directory.Exists(rule.Destination))
                {
                    _logger.LogDebug("Auto-move rule found for {Extension}: {Destination}", item.Extension, rule.Destination);
                    autoMoveItems.Add((item, rule));
                    continue;
                }
            }
            manualItems.Add(item);
        }

        _logger.LogDebug("Auto-move: {AutoCount}, Manual: {ManualCount}", autoMoveItems.Count, manualItems.Count);

        // Process auto-move items silently
        if (autoMoveItems.Count > 0)
        {
            await ProcessAutoMoveItemsAsync(autoMoveItems);
        }

        // If there are items without auto-move rules, show the popup
        if (manualItems.Count > 0)
        {
            DroppedItems.Clear();
            foreach (var item in manualItems)
            {
                DroppedItems.Add(item);
            }

            CurrentItem = DroppedItems[0];
            
            if (DroppedItems.Count == 1)
            {
                await LoadSuggestionsAsync(CurrentItem);
            }
            else
            {
                StatusText = $"{DroppedItems.Count} items selected";
                await LoadSuggestionsAsync(CurrentItem);
            }

            IsPopupOpen = true;
            _logger.LogDebug("Popup opened for manual selection");
        }
        else
        {
            // All items were auto-moved, reset state
            _logger.LogDebug("All items auto-moved, resetting state");
            ResetState();
        }
    }

    /// <summary>
    /// Processes items with auto-move rules silently in the background.
    /// </summary>
    private async Task ProcessAutoMoveItemsAsync(List<(DroppedItem Item, FileRule Rule)> items)
    {
        _logger.LogInformation("Processing {Count} auto-move items", items.Count);
        
        foreach (var (item, rule) in items)
        {
            try
            {
                var operation = await _fileOperationService.MoveAsync(item.FullPath, rule.Destination);
                LastOperation = operation;

                // Update rule usage statistics
                await _ruleService.UpdateRuleUsageAsync(item.Extension);

                // Show success notification for auto-move
                _notificationService.ShowAutoMoveSuccess(
                    operation.ItemName,
                    Path.GetFileName(rule.Destination),
                    () => UndoLastOperationCommand.Execute(null));
                    
                _logger.LogInformation("Auto-moved: {ItemName} -> {Destination}", item.Name, rule.Destination);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-move failed for {ItemName}", item.Name);
                _notificationService.ShowError($"Auto-move failed: {item.Name}", ex.Message);
            }
        }
    }

    /// <summary>
    /// Handles drag enter event.
    /// </summary>
    [RelayCommand]
    private void HandleDragEnter()
    {
        IsDragOver = true;
        StatusText = "Release to drop";
    }

    /// <summary>
    /// Handles drag leave event.
    /// </summary>
    [RelayCommand]
    private void HandleDragLeave()
    {
        IsDragOver = false;
        StatusText = "Drop files here";
    }

    /// <summary>
    /// Moves items to the selected destination.
    /// </summary>
    [RelayCommand]
    private async Task MoveToDestinationAsync(DestinationSuggestion suggestion)
    {
        if (suggestion == null || DroppedItems.Count == 0)
            return;

        _logger.LogInformation("Moving {Count} items to: {Destination}", DroppedItems.Count, suggestion.FullPath);

        IsBusy = true;
        IsPopupOpen = false;

        try
        {
            foreach (var item in DroppedItems)
            {
                // Check if this extension has an auto-move rule
                if (!item.IsDirectory)
                {
                    var existingRule = await _ruleService.GetRuleForExtensionAsync(item.Extension);
                    if (existingRule != null)
                    {
                        await _ruleService.UpdateRuleUsageAsync(item.Extension);
                    }
                }

                var operation = await _fileOperationService.MoveAsync(item.FullPath, suggestion.FullPath);
                LastOperation = operation;

                // Save rule if "Remember" is checked (with optional auto-move)
                if (RememberChoice && !item.IsDirectory && !string.IsNullOrEmpty(item.Extension))
                {
                    _logger.LogDebug("Saving rule for {Extension}: AutoMove={AutoMove}", item.Extension, EnableAutoMove);
                    await _ruleService.SaveRuleAsync(item.Extension, suggestion.FullPath, EnableAutoMove);
                }

                _notificationService.ShowMoveSuccess(operation, () => UndoLastOperationCommand.Execute(null));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Move operation failed");
            _notificationService.ShowError("Move Failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
            ResetState();
        }
    }

    /// <summary>
    /// Opens folder browser dialog for custom destination.
    /// </summary>
    [RelayCommand]
    private async Task BrowseDestinationAsync()
    {
        _logger.LogDebug("Opening folder browser dialog");
        
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Destination Folder"
        };

        if (dialog.ShowDialog() == true)
        {
            _logger.LogDebug("User selected folder: {Folder}", dialog.FolderName);
            
            var suggestion = new DestinationSuggestion
            {
                DisplayName = Path.GetFileName(dialog.FolderName),
                FullPath = dialog.FolderName,
                IsRecommended = false,
                IsFromRule = false,
                Confidence = 100
            };

            await MoveToDestinationAsync(suggestion);
        }
    }

    /// <summary>
    /// Undoes the last move operation.
    /// </summary>
    [RelayCommand]
    private async Task UndoLastOperationAsync()
    {
        if (LastOperation == null || !LastOperation.CanUndo)
            return;

        _logger.LogInformation("Undoing operation for: {ItemName}", LastOperation.ItemName);

        try
        {
            var success = await _fileOperationService.UndoMoveAsync(LastOperation);
            if (success)
            {
                _notificationService.ShowUndoSuccess(LastOperation.ItemName);
                _logger.LogInformation("Undo successful for: {ItemName}", LastOperation.ItemName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Undo failed for: {ItemName}", LastOperation.ItemName);
            _notificationService.ShowError("Undo Failed", ex.Message);
        }
        finally
        {
            LastOperation = null;
        }
    }

    /// <summary>
    /// Cancels the current operation and closes the popup.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        _logger.LogDebug("Operation cancelled by user");
        IsPopupOpen = false;
        ResetState();
    }

    private async Task LoadSuggestionsAsync(DroppedItem item)
    {
        _logger.LogDebug("Loading suggestions for: {ItemName}", item.Name);
        
        var suggestions = await _suggestionService.GetSuggestionsAsync(item);
        
        Suggestions.Clear();
        foreach (var suggestion in suggestions)
        {
            Suggestions.Add(suggestion);
        }

        _logger.LogDebug("Loaded {Count} suggestions", Suggestions.Count);
        StatusText = $"{item.Name} ({item.Category})";
    }

    private void ResetState()
    {
        DroppedItems.Clear();
        Suggestions.Clear();
        CurrentItem = null;
        RememberChoice = false;
        EnableAutoMove = false;
        IsDragOver = false;
        StatusText = "Drop files here";
    }
}
