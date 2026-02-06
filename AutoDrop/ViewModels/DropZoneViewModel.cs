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
public partial class DropZoneViewModel : Base.ViewModelBase, IDisposable
{
    private readonly IFileOperationService _fileOperationService;
    private readonly IDestinationSuggestionService _suggestionService;
    private readonly IRuleService _ruleService;
    private readonly INotificationService _notificationService;
    private readonly IUndoService _undoService;
    private readonly IDuplicateDetectionService _duplicateDetectionService;
    private readonly IAiService _aiService;
    private readonly ISettingsService _settingsService;
    private readonly IWindowService _windowService;
    private readonly IHistoryService _historyService;
    private readonly ILogger<DropZoneViewModel> _logger;
    private bool _disposed;
    private DuplicateHandling _currentDuplicateHandling = DuplicateHandling.Ask;
    
    /// <summary>
    /// Cancellation token source for AI analysis operations.
    /// Cancelled when new items are dropped or state is reset.
    /// </summary>
    private CancellationTokenSource? _aiAnalysisCts;
    
    /// <summary>
    /// The item currently being analyzed by AI. Used to prevent stale updates.
    /// </summary>
    private DroppedItem? _currentAiAnalysisItem;

    /// <summary>
    /// Threshold for showing batch operation dialog.
    /// </summary>
    private const int BatchOperationThreshold = 3;

    [ObservableProperty]
    private bool _isDragOver;

    [ObservableProperty]
    private bool _isPopupOpen;

    [ObservableProperty]
    private string _statusText = "Drop files here";

    [ObservableProperty]
    private DroppedItem? _currentItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUndo))]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    private bool _isUndoAvailable;

    [ObservableProperty]
    private string _undoDescription = string.Empty;

    [ObservableProperty]
    private string _undoTitle = "File moved";

    [ObservableProperty]
    private int _undoCount;

    /// <summary>
    /// Whether the current drop contains only files (no directories).
    /// Used to show/hide the extension rule settings.
    /// </summary>
    [ObservableProperty]
    private bool _hasFilesOnly;

    public bool CanUndo => IsUndoAvailable;

    public ObservableCollection<DroppedItem> DroppedItems { get; } = [];
    public ObservableCollection<DestinationSuggestion> Suggestions { get; } = [];
    
    /// <summary>
    /// Collection of extension-specific rule settings for the current drop.
    /// Each unique extension gets its own checkbox in the UI.
    /// </summary>
    public ObservableCollection<ExtensionRuleSetting> ExtensionRuleSettings { get; } = [];

    /// <summary>
    /// AI analysis result for the current item (if available).
    /// </summary>
    [ObservableProperty]
    private AiAnalysisResult? _aiAnalysisResult;

    /// <summary>
    /// Whether AI is currently analyzing the file.
    /// </summary>
    [ObservableProperty]
    private bool _isAiAnalyzing;

    /// <summary>
    /// Event raised when batch operation dialog should be shown.
    /// </summary>
    public event EventHandler<BatchOperationRequestedEventArgs>? BatchOperationRequested;

    /// <summary>
    /// Event raised when a duplicate file is detected and user input is needed.
    /// </summary>
    public event EventHandler<DuplicateHandlingRequestedEventArgs>? DuplicateHandlingRequested;

    public DropZoneViewModel(
        IFileOperationService fileOperationService,
        IDestinationSuggestionService suggestionService,
        IRuleService ruleService,
        INotificationService notificationService,
        IUndoService undoService,
        IDuplicateDetectionService duplicateDetectionService,
        IAiService aiService,
        ISettingsService settingsService,
        IWindowService windowService,
        IHistoryService historyService,
        ILogger<DropZoneViewModel> logger)
    {
        _fileOperationService = fileOperationService ?? throw new ArgumentNullException(nameof(fileOperationService));
        _suggestionService = suggestionService ?? throw new ArgumentNullException(nameof(suggestionService));
        _ruleService = ruleService ?? throw new ArgumentNullException(nameof(ruleService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _undoService = undoService ?? throw new ArgumentNullException(nameof(undoService));
        _duplicateDetectionService = duplicateDetectionService ?? throw new ArgumentNullException(nameof(duplicateDetectionService));
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
        _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
        _logger = logger;

        // Subscribe to undo events
        _undoService.UndoAvailable += OnUndoAvailable;
        _undoService.UndoExecuted += OnUndoExecuted;
        
        _logger.LogDebug("DropZoneViewModel initialized");
    }

    private void OnUndoAvailable(object? sender, UndoAvailableEventArgs e)
    {
        // Ensure UI update on main thread
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            IsUndoAvailable = true;
            UndoCount = e.TotalCount;
            UndoDescription = e.Description;
            UndoTitle = e.TotalCount == 1 ? "File moved" : $"{e.TotalCount} files moved";
            _logger.LogDebug("Undo available: {Description}, count: {Count}", e.Description, e.TotalCount);
        });
        
        // Auto-clear after expiration
        Task.Delay(TimeSpan.FromSeconds(e.ExpirationSeconds)).ContinueWith(_ =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                if (IsUndoAvailable && UndoDescription == e.Description)
                {
                    IsUndoAvailable = false;
                    UndoDescription = string.Empty;
                    UndoTitle = "File moved";
                    UndoCount = 0;
                }
            });
        });
    }

    private void OnUndoExecuted(object? sender, UndoExecutedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            IsUndoAvailable = false;
            UndoDescription = string.Empty;
            UndoTitle = "File moved";
            UndoCount = 0;

            if (e.Success)
            {
                var message = e.UndoneCount == 1 
                    ? e.Description 
                    : $"{e.UndoneCount} items";
                _notificationService.ShowUndoSuccess(message);
            }
            else if (!string.IsNullOrEmpty(e.ErrorMessage))
            {
                _notificationService.ShowError("Undo Failed", e.ErrorMessage);
            }
        });
    }

    /// <summary>
    /// Handles files being dropped onto the drop zone.
    /// Automatically moves files if an auto-move rule exists.
    /// For single folder drops, opens the folder organization window.
    /// </summary>
    [RelayCommand]
    private async Task HandleDropAsync(string[] paths)
    {
        if (paths is not { Length: > 0 })
            return;

        _logger.LogInformation("Files dropped: {Count} items", paths.Length);

        // Check if a single folder was dropped - show folder organization window
        if (paths.Length == 1 && Directory.Exists(paths[0]))
        {
            _logger.LogInformation("Single folder dropped, opening folder organization: {Folder}", paths[0]);
            _windowService.ShowFolderOrganization(paths[0]);
            return;
        }

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

        // If there are items without auto-move rules, decide between batch or regular popup
        if (manualItems.Count > 0)
        {
            // Check if batch operation dialog should be shown
            if (ShouldShowBatchOperationDialog(manualItems))
            {
                _logger.LogInformation("Triggering batch operation dialog for {Count} items", manualItems.Count);
                var args = new BatchOperationRequestedEventArgs(manualItems);
                BatchOperationRequested?.Invoke(this, args);
                
                if (args.Handled)
                {
                    _logger.LogDebug("Batch operation handled by subscriber");
                    ResetState();
                    return;
                }
                
                // If not handled, fall through to regular popup
                _logger.LogDebug("Batch operation not handled, falling back to regular popup");
            }
            
            DroppedItems.Clear();
            foreach (var item in manualItems)
            {
                DroppedItems.Add(item);
            }

            CurrentItem = DroppedItems[0];
            
            // Build extension rule settings for each unique file extension
            // This allows users to create rules for each extension independently
            ExtensionRuleSettings.Clear();
            var extensionGroups = DroppedItems
                .Where(i => !i.IsDirectory && !string.IsNullOrEmpty(i.Extension))
                .GroupBy(i => i.Extension.ToLowerInvariant())
                .ToList();
            
            HasFilesOnly = DroppedItems.All(i => !i.IsDirectory);
            
            foreach (var group in extensionGroups)
            {
                ExtensionRuleSettings.Add(new ExtensionRuleSetting
                {
                    Extension = group.Key,
                    FileCount = group.Count()
                });
            }
            
            _logger.LogDebug("Created {Count} extension rule settings for extensions: {Extensions}", 
                ExtensionRuleSettings.Count, 
                string.Join(", ", ExtensionRuleSettings.Select(e => e.Extension)));
            
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
    /// Determines whether the batch operation dialog should be shown for the given items.
    /// Shows batch dialog when there are multiple items with different categories.
    /// </summary>
    /// <param name="items">The items to check.</param>
    /// <returns>True if batch operation dialog should be shown.</returns>
    private bool ShouldShowBatchOperationDialog(List<DroppedItem> items)
    {
        // Show batch dialog when there are enough items
        if (items.Count < BatchOperationThreshold)
        {
            _logger.LogDebug("Item count {Count} below threshold {Threshold}", items.Count, BatchOperationThreshold);
            return false;
        }

        // Count unique categories for informational purposes
        var uniqueCategories = items
            .Select(i => i.Category)
            .Distinct()
            .Count();

        _logger.LogDebug(
            "Batch dialog check: {ItemCount} items, {CategoryCount} categories - showing batch dialog",
            items.Count, uniqueCategories);

        // Show batch dialog for 3+ files regardless of category
        return true;
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

                // Record operation in history
                await _historyService.RecordOperationAsync(
                    operation.SourcePath, 
                    operation.DestinationPath, 
                    OperationType.Move);

                // Register undo operation
                RegisterUndoForOperation(operation);

                // Update rule usage statistics
                await _ruleService.UpdateRuleUsageAsync(item.Extension);

                // Show success notification
                _notificationService.ShowAutoMoveSuccess(
                    operation.ItemName,
                    Path.GetFileName(rule.Destination));
                    
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

        // Reset duplicate handling for new batch
        _currentDuplicateHandling = DuplicateHandling.Ask;
        var itemsToProcess = DroppedItems.ToList();
        var skippedCount = 0;

        try
        {
            for (var i = 0; i < itemsToProcess.Count; i++)
            {
                var item = itemsToProcess[i];
                var hasMoreItems = i < itemsToProcess.Count - 1;

                // Check for duplicates (only for files, not directories)
                if (!item.IsDirectory && _duplicateDetectionService.IsEnabled)
                {
                    var destFilePath = Path.Combine(suggestion.FullPath, item.Name);
                    
                    if (File.Exists(destFilePath))
                    {
                        var duplicateResult = await _duplicateDetectionService.CheckForDuplicateAsync(
                            item.FullPath, destFilePath);

                        if (duplicateResult.IsDuplicate)
                        {
                            var handling = await HandleDuplicateAsync(duplicateResult, hasMoreItems);
                            
                            if (handling == null)
                            {
                                // User cancelled
                                _logger.LogInformation("Move operation cancelled by user");
                                return;
                            }

                            switch (handling.Value)
                            {
                                case DuplicateHandling.SkipAll:
                                    _logger.LogDebug("Skipping duplicate: {Item}", item.Name);
                                    skippedCount++;
                                    continue;

                                case DuplicateHandling.DeleteSourceAll when duplicateResult.IsExactMatch:
                                    _logger.LogDebug("Deleting source (exact duplicate): {Item}", item.Name);
                                    File.Delete(item.FullPath);
                                    skippedCount++;
                                    continue;

                                case DuplicateHandling.ReplaceAll:
                                    _logger.LogDebug("Replacing destination: {Item}", item.Name);
                                    File.Delete(destFilePath);
                                    break;

                                // KeepBothAll falls through to normal move (auto-rename)
                            }
                        }
                    }
                }

                // Check if this extension has an auto-move rule
                if (!item.IsDirectory)
                {
                    var existingRule = await _ruleService.GetRuleForExtensionAsync(item.Extension);
                    if (existingRule != null)
                    {
                        await _ruleService.UpdateRuleUsageAsync(item.Extension);
                    }
                }

                // Pass AI-suggested filename if smart rename is enabled and we have a suggestion
                string? suggestedFileName = null;
                if (AiAnalysisResult is { Success: true, SuggestedName: not null })
                {
                    var settings = await _settingsService.GetSettingsAsync();
                    if (settings.AiSettings.EnableSmartRename)
                    {
                        suggestedFileName = AiAnalysisResult.SuggestedName;
                    }
                }

                var operation = await _fileOperationService.MoveAsync(item.FullPath, suggestion.FullPath, suggestedFileName);

                // Record operation in history with AI confidence
                await _historyService.RecordOperationAsync(
                    operation.SourcePath, 
                    operation.DestinationPath, 
                    OperationType.Move,
                    AiAnalysisResult?.Confidence);

                // Register undo operation
                RegisterUndoForOperation(operation);

                // Save rule based on per-extension settings
                // Each extension has its own checkbox, so we check if the user enabled "Remember" for THIS extension
                if (!item.IsDirectory && !string.IsNullOrEmpty(item.Extension))
                {
                    var extensionSetting = ExtensionRuleSettings
                        .FirstOrDefault(e => string.Equals(e.Extension, item.Extension, StringComparison.OrdinalIgnoreCase));
                    
                    if (extensionSetting is { RememberChoice: true })
                    {
                        _logger.LogDebug("Saving rule for {Extension}: AutoMove={AutoMove}", 
                            item.Extension, extensionSetting.EnableAutoMove);
                        await _ruleService.SaveRuleAsync(item.Extension, suggestion.FullPath, extensionSetting.EnableAutoMove);
                    }
                }

                _notificationService.ShowMoveSuccess(operation);
            }

            if (skippedCount > 0)
            {
                _notificationService.ShowWarning("Some files skipped", 
                    $"{skippedCount} duplicate file(s) were skipped.");
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
    /// Handles a detected duplicate by asking the user or using the remembered choice.
    /// </summary>
    private async Task<DuplicateHandling?> HandleDuplicateAsync(DuplicateCheckResult duplicateResult, bool hasMoreDuplicates)
    {
        // If we already have a "apply to all" choice, use it
        if (_currentDuplicateHandling != DuplicateHandling.Ask)
        {
            return _currentDuplicateHandling;
        }

        // Ask the user via event
        var args = new DuplicateHandlingRequestedEventArgs(duplicateResult, hasMoreDuplicates);
        
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            DuplicateHandlingRequested?.Invoke(this, args);
        });

        if (!args.Handled || args.WasCancelled)
        {
            return null;
        }

        // Remember choice if "apply to all" was selected
        if (args.ApplyToAll)
        {
            _currentDuplicateHandling = args.SelectedHandling;
        }

        return args.SelectedHandling;
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
    /// Executes the undo operation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private async Task UndoAsync()
    {
        _logger.LogInformation("User initiated undo");
        await _undoService.ExecuteUndoAsync();
    }

    /// <summary>
    /// Registers an undo operation for a completed move.
    /// </summary>
    private void RegisterUndoForOperation(MoveOperation operation)
    {
        _undoService.RegisterOperation(
            operation.ItemName,
            async () => await _fileOperationService.UndoMoveAsync(operation),
            expirationSeconds: 10);
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
        
        // Load regular suggestions first
        var suggestions = await _suggestionService.GetSuggestionsAsync(item);
        
        Suggestions.Clear();
        foreach (var suggestion in suggestions)
        {
            Suggestions.Add(suggestion);
        }

        _logger.LogDebug("Loaded {Count} suggestions", Suggestions.Count);
        StatusText = $"{item.Name} ({item.Category})";

        // Cancel any previous AI analysis and start new one
        CancelAiAnalysis();
        _aiAnalysisCts = new CancellationTokenSource();
        _currentAiAnalysisItem = item;
        
        // Run AI analysis with proper cancellation support
        _ = RunAiAnalysisAsync(item, _aiAnalysisCts.Token);
    }

    /// <summary>
    /// Cancels any ongoing AI analysis operation.
    /// </summary>
    private void CancelAiAnalysis()
    {
        if (_aiAnalysisCts != null)
        {
            _aiAnalysisCts.Cancel();
            _aiAnalysisCts.Dispose();
            _aiAnalysisCts = null;
        }
        _currentAiAnalysisItem = null;
    }

    /// <summary>
    /// Runs AI analysis on the dropped item and adds AI-based suggestions.
    /// Prioritizes matching to user's existing custom folders before suggesting new ones.
    /// </summary>
    private async Task RunAiAnalysisAsync(DroppedItem item, CancellationToken ct)
    {
        try
        {
            // Check cancellation early
            ct.ThrowIfCancellationRequested();
            
            // Check if AI is enabled
            var settings = await _settingsService.GetSettingsAsync();
            if (!settings.AiSettings.IsFullyConfigured)
            {
                _logger.LogDebug("[AI] AI not configured, skipping analysis");
                return;
            }

            // Only analyze files, not directories
            if (item.IsDirectory)
            {
                _logger.LogDebug("[AI] Skipping AI analysis for directory");
                return;
            }

            // THREAD SAFETY: Update UI on dispatcher
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsAiAnalyzing = true;
                AiAnalysisResult = null;
            });
            
            _logger.LogInformation("[AI] 🚀 Starting AI analysis for: {Name}", item.Name);
            
            // Check cancellation before expensive operation
            ct.ThrowIfCancellationRequested();
            
            // Get custom folders to pass to AI for matching
            var customFolders = await _settingsService.GetCustomFoldersAsync();
            _logger.LogDebug("[AI] Passing {Count} custom folders to AI for matching", customFolders.Count);
            
            var result = await _aiService.AnalyzeFileAsync(item.FullPath, customFolders, ct);
            
            // STALE UPDATE CHECK: Verify this is still the current item being analyzed
            if (_currentAiAnalysisItem != item || ct.IsCancellationRequested)
            {
                _logger.LogDebug("[AI] Analysis result discarded - item changed or cancelled");
                return;
            }
            
            // THREAD SAFETY: Update UI on dispatcher
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Double-check we're still analyzing the same item
                if (_currentAiAnalysisItem != item)
                {
                    _logger.LogDebug("[AI] Discarding stale AI result - current item changed");
                    return;
                }
                
                AiAnalysisResult = result;
                IsAiAnalyzing = false;
                
                if (result.Success)
                {
                    // Update status with AI info
                    var aiInfo = result.SuggestedName != null 
                        ? $"✨ AI: {result.Category} • Rename: {result.SuggestedName}" 
                        : $"✨ AI: {result.Category}";
                    StatusText = aiInfo;
                    
                    // Check confidence threshold
                    if (result.Confidence < settings.AiSettings.ConfidenceThreshold)
                    {
                        _logger.LogDebug("[AI] Confidence {Confidence:P0} below threshold {Threshold:P0}", 
                            result.Confidence, settings.AiSettings.ConfidenceThreshold);
                        return;
                    }

                    // PRIORITY 1: If AI matched to an existing custom folder, use it
                    if (result.HasMatchedFolder && !string.IsNullOrEmpty(result.MatchedFolderPath))
                    {
                        var matchedSuggestion = new DestinationSuggestion
                        {
                            DisplayName = $"✨ {result.MatchedFolderName ?? result.Category}",
                            FullPath = NormalizePath(result.MatchedFolderPath),
                            IsRecommended = true,
                            IsFromRule = false,
                            Confidence = (int)(result.Confidence * 100),
                            IsAiSuggestion = true,
                            IsNewFolder = false
                        };
                        
                        Suggestions.Insert(0, matchedSuggestion);
                        
                        _logger.LogInformation("[AI] ✅ Matched to existing folder: {FolderName} at {Path} (Confidence: {Confidence:P0})", 
                            result.MatchedFolderName, result.MatchedFolderPath, result.Confidence);
                    }
                    // PRIORITY 2: If no match, suggest a new folder path
                    else if (!string.IsNullOrEmpty(result.Category))
                    {
                        // Use user's configured base path for new folders
                        var basePath = settings.AiSettings.ResolvedNewFolderBasePath;
                        
                        // Sanitize the category name for file system
                        var safeFolderName = SanitizeFolderName(result.Category);
                        var aiSuggestedPath = Path.Combine(basePath, safeFolderName);
                        
                        var aiSuggestion = new DestinationSuggestion
                        {
                            DisplayName = $"📁 Create: {result.Category}",
                            FullPath = aiSuggestedPath,
                            IsRecommended = true,
                            IsFromRule = false,
                            Confidence = (int)(result.Confidence * 100),
                            IsAiSuggestion = true,
                            IsNewFolder = true,
                            SuggestedFolderName = safeFolderName
                        };
                        
                        Suggestions.Insert(0, aiSuggestion);
                        
                        _logger.LogInformation("[AI] ✅ Suggesting new folder: {Category} at {Path} (Confidence: {Confidence:P0})", 
                            result.Category, aiSuggestedPath, result.Confidence);
                    }
                }
                else
                {
                    _logger.LogWarning("[AI] Analysis failed: {Error}", result.Error);
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Expected when analysis is cancelled - not an error
            _logger.LogDebug("[AI] Analysis cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AI] Error during AI analysis");
        }
        finally
        {
            // THREAD SAFETY: Always update UI on dispatcher
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Only clear analyzing state if this is still the active analysis
                if (_currentAiAnalysisItem == item)
                {
                    IsAiAnalyzing = false;
                }
            });
        }
    }

    private void ResetState()
    {
        // Cancel any pending AI analysis
        CancelAiAnalysis();
        
        DroppedItems.Clear();
        Suggestions.Clear();
        ExtensionRuleSettings.Clear();
        CurrentItem = null;
        HasFilesOnly = false;
        IsDragOver = false;
        IsAiAnalyzing = false;
        AiAnalysisResult = null;
        StatusText = "Drop files here";
    }

    /// <summary>
    /// Normalizes a path by replacing forward slashes with the system's directory separator.
    /// </summary>
    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        return path.Replace('/', Path.DirectorySeparatorChar)
                   .Replace('\\', Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// Sanitizes a folder name by removing invalid characters.
    /// </summary>
    private static string SanitizeFolderName(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName)) return "Uncategorized";
        
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(folderName
            .Where(c => !invalidChars.Contains(c))
            .ToArray());
        
        return string.IsNullOrWhiteSpace(sanitized) ? "Uncategorized" : sanitized.Trim();
    }

    /// <summary>
    /// Disposes resources and unsubscribes from events.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        
        // Cancel any pending AI analysis
        CancelAiAnalysis();
        
        _undoService.UndoAvailable -= OnUndoAvailable;
        _undoService.UndoExecuted -= OnUndoExecuted;
        _disposed = true;
        
        _logger.LogDebug("DropZoneViewModel disposed");
    }
}
