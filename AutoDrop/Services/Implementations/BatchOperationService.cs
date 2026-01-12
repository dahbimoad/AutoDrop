using AutoDrop.Models;
using AutoDrop.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoDrop.Services.Implementations;

/// <summary>
/// Implementation of batch file operation service.
/// </summary>
public sealed class BatchOperationService : IBatchOperationService
{
    private readonly IFileOperationService _fileOperationService;
    private readonly IDestinationSuggestionService _suggestionService;
    private readonly IDuplicateDetectionService _duplicateDetectionService;
    private readonly IRuleService _ruleService;
    private readonly ILogger<BatchOperationService> _logger;

    /// <inheritdoc />
    public event EventHandler<DuplicateDetectedEventArgs>? DuplicateDetected;

    public BatchOperationService(
        IFileOperationService fileOperationService,
        IDestinationSuggestionService suggestionService,
        IDuplicateDetectionService duplicateDetectionService,
        IRuleService ruleService,
        ILogger<BatchOperationService> logger)
    {
        _fileOperationService = fileOperationService ?? throw new ArgumentNullException(nameof(fileOperationService));
        _suggestionService = suggestionService ?? throw new ArgumentNullException(nameof(suggestionService));
        _duplicateDetectionService = duplicateDetectionService ?? throw new ArgumentNullException(nameof(duplicateDetectionService));
        _ruleService = ruleService ?? throw new ArgumentNullException(nameof(ruleService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("BatchOperationService initialized");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BatchFileGroup>> GroupItemsByDestinationAsync(
        IEnumerable<DroppedItem> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var itemsList = items.ToList();
        if (itemsList.Count == 0)
        {
            return Array.Empty<BatchFileGroup>();
        }

        _logger.LogDebug("Grouping {Count} items by extension", itemsList.Count);

        // Group by EXTENSION only - user will select destination in the UI
        var groups = new Dictionary<string, BatchFileGroup>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in itemsList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Create group key based on extension only
            var extension = item.IsDirectory ? "folder" : item.Extension.ToLowerInvariant();

            if (!groups.TryGetValue(extension, out var group))
            {
                group = new BatchFileGroup
                {
                    Category = item.Category,
                    Extension = extension,
                    // Destination will be set by user in UI - leave empty for now
                    DestinationPath = string.Empty,
                    DestinationDisplayName = string.Empty,
                    Items = []
                };
                groups[extension] = group;
            }

            group.Items.Add(item);
        }

        var result = groups.Values
            .OrderByDescending(g => g.FileCount)
            .ThenBy(g => g.Extension)
            .ToList();

        _logger.LogDebug("Created {Count} extension groups from {Total} items", result.Count, itemsList.Count);

        return await Task.FromResult(result);
    }

    /// <inheritdoc />
    public async Task<BatchOperationResult> ExecuteBatchMoveAsync(
        IEnumerable<BatchFileGroup> groups,
        IProgress<BatchProgressReport>? progress = null,
        DuplicateHandling duplicateHandling = DuplicateHandling.KeepBothAll,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groups);

        var groupsList = groups.Where(g => g.IsSelected).ToList();
        var allItems = groupsList.SelectMany(g => g.Items).ToList();

        if (allItems.Count == 0)
        {
            _logger.LogDebug("No items to process in batch operation");
            return new BatchOperationResult { TotalItems = 0 };
        }

        _logger.LogInformation("Starting batch move for {Count} items in {Groups} groups",
            allItems.Count, groupsList.Count);

        var operations = new List<MoveOperation>();
        var errors = new List<BatchOperationError>();
        var successCount = 0;
        var skippedCount = 0;
        var currentDuplicateHandling = duplicateHandling;
        var destinationPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < groupsList.Count; i++)
        {
            var group = groupsList[i];
            destinationPaths.Add(group.DestinationPath);

            foreach (var item in group.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var itemIndex = allItems.IndexOf(item) + 1;
                progress?.Report(new BatchProgressReport
                {
                    CurrentItem = item.Name,
                    CurrentIndex = itemIndex,
                    TotalItems = allItems.Count,
                    Status = $"Moving to {group.DestinationDisplayName}..."
                });

                try
                {
                    // Check for duplicates if it's a file (not directory)
                    if (!item.IsDirectory)
                    {
                        var destFilePath = Path.Combine(group.DestinationPath, item.Name);
                        var duplicateResult = await _duplicateDetectionService
                            .CheckForDuplicateAsync(item.FullPath, destFilePath, cancellationToken)
                            .ConfigureAwait(false);

                        if (duplicateResult.IsDuplicate)
                        {
                            _logger.LogDebug("Duplicate detected for {Item}: {Method}",
                                item.Name, duplicateResult.ComparisonMethod);

                            // Handle duplicate based on current handling mode
                            var handling = await HandleDuplicateAsync(
                                duplicateResult, 
                                currentDuplicateHandling, 
                                cancellationToken).ConfigureAwait(false);

                            currentDuplicateHandling = handling.ApplyToAll 
                                ? handling.UserDecision 
                                : currentDuplicateHandling;

                            switch (handling.UserDecision)
                            {
                                case DuplicateHandling.SkipAll:
                                    _logger.LogDebug("Skipping duplicate: {Item}", item.Name);
                                    skippedCount++;
                                    continue;

                                case DuplicateHandling.DeleteSourceAll when duplicateResult.IsExactMatch:
                                    // Delete source since destination has same content
                                    _logger.LogDebug("Deleting source (exact duplicate): {Item}", item.Name);
                                    File.Delete(item.FullPath);
                                    skippedCount++;
                                    continue;

                                case DuplicateHandling.ReplaceAll:
                                    // Delete existing file before move
                                    _logger.LogDebug("Replacing destination: {Item}", item.Name);
                                    File.Delete(destFilePath);
                                    break;

                                // KeepBothAll and Ask fall through to normal move (auto-rename)
                            }
                        }
                    }

                    // Perform the move
                    var operation = await _fileOperationService
                        .MoveAsync(item.FullPath, group.DestinationPath, cancellationToken)
                        .ConfigureAwait(false);

                    operations.Add(operation);
                    successCount++;

                    // Update rule usage if applicable
                    if (!item.IsDirectory && !string.IsNullOrEmpty(item.Extension))
                    {
                        await _ruleService.UpdateRuleUsageAsync(item.Extension).ConfigureAwait(false);
                    }

                    _logger.LogDebug("Moved {Item} to {Dest}", item.Name, group.DestinationPath);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to move {Item} to {Dest}", item.Name, group.DestinationPath);

                    errors.Add(new BatchOperationError
                    {
                        ItemName = item.Name,
                        SourcePath = item.FullPath,
                        DestinationPath = group.DestinationPath,
                        ErrorMessage = ex.Message
                    });
                }
            }
        }

        progress?.Report(new BatchProgressReport
        {
            CurrentItem = "Complete",
            CurrentIndex = allItems.Count,
            TotalItems = allItems.Count,
            Status = "Batch operation complete"
        });

        var result = new BatchOperationResult
        {
            TotalItems = allItems.Count,
            SuccessCount = successCount,
            FailedCount = errors.Count,
            SkippedCount = skippedCount,
            DestinationCount = destinationPaths.Count,
            Errors = errors,
            Operations = operations
        };

        _logger.LogInformation("Batch move completed: {Summary}", result.GetSummaryMessage());

        return result;
    }

    /// <inheritdoc />
    public async Task<int> UndoBatchAsync(
        IEnumerable<MoveOperation> operations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var operationsList = operations.ToList();
        if (operationsList.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation("Undoing batch of {Count} operations", operationsList.Count);

        var successCount = 0;

        // Undo in reverse order (LIFO)
        foreach (var operation in operationsList.AsEnumerable().Reverse())
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var success = await _fileOperationService
                    .UndoMoveAsync(operation, cancellationToken)
                    .ConfigureAwait(false);

                if (success)
                {
                    successCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to undo operation for {Item}", operation.ItemName);
            }
        }

        _logger.LogInformation("Batch undo completed: {Success}/{Total}", successCount, operationsList.Count);

        return successCount;
    }

    /// <summary>
    /// Gets the best destination for an item based on rules or suggestions.
    /// </summary>
    private async Task<DestinationSuggestion> GetBestDestinationAsync(
        DroppedItem item,
        CancellationToken cancellationToken)
    {
        // First check if there's a rule for this extension
        if (!item.IsDirectory && !string.IsNullOrEmpty(item.Extension))
        {
            var rule = await _ruleService.GetRuleForExtensionAsync(item.Extension).ConfigureAwait(false);
            if (rule is { IsEnabled: true } && Directory.Exists(rule.Destination))
            {
                return new DestinationSuggestion
                {
                    DisplayName = Path.GetFileName(rule.Destination),
                    FullPath = rule.Destination,
                    IsFromRule = true,
                    IsRecommended = true,
                    Confidence = 100
                };
            }
        }

        // Fall back to suggestions
        var suggestions = await _suggestionService.GetSuggestionsAsync(item).ConfigureAwait(false);
        return suggestions.FirstOrDefault() ?? new DestinationSuggestion
        {
            DisplayName = "Downloads",
            FullPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads",
            IsRecommended = false,
            Confidence = 50
        };
    }

    /// <summary>
    /// Handles a duplicate detection result.
    /// </summary>
    private Task<DuplicateDetectedEventArgs> HandleDuplicateAsync(
        DuplicateCheckResult duplicateResult,
        DuplicateHandling currentHandling,
        CancellationToken cancellationToken)
    {
        // If we already have a handling decision (not Ask), use it
        if (currentHandling != DuplicateHandling.Ask)
        {
            return Task.FromResult(new DuplicateDetectedEventArgs
            {
                SourceFile = duplicateResult.Source,
                DestinationFile = duplicateResult.Destination,
                IsExactMatch = duplicateResult.IsExactMatch,
                UserDecision = currentHandling,
                ApplyToAll = true
            });
        }

        // Raise event to get user decision
        var args = new DuplicateDetectedEventArgs
        {
            SourceFile = duplicateResult.Source,
            DestinationFile = duplicateResult.Destination,
            IsExactMatch = duplicateResult.IsExactMatch,
            UserDecision = DuplicateHandling.KeepBothAll, // Default
            ApplyToAll = false
        };

        DuplicateDetected?.Invoke(this, args);

        return Task.FromResult(args);
    }
}
