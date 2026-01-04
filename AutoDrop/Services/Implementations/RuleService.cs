using AutoDrop.Models;
using AutoDrop.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoDrop.Services.Implementations;

/// <summary>
/// Implementation of rule service for managing file extension rules.
/// </summary>
public sealed class RuleService : IRuleService, IDisposable
{
    private readonly IStorageService _storageService;
    private readonly ILogger<RuleService> _logger;
    private RulesConfiguration? _cachedConfiguration;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    public RuleService(IStorageService storageService, ILogger<RuleService> logger)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _logger = logger;
        _logger.LogDebug("RuleService initialized");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileRule>> GetAllRulesAsync(CancellationToken cancellationToken = default)
    {
        var config = await GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Retrieved {Count} rules", config.Rules.Count);
        return config.Rules.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<FileRule?> GetRuleForExtensionAsync(string extension, CancellationToken cancellationToken = default)
    {
        var normalizedExtension = NormalizeExtension(extension);
        var config = await GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        
        var rule = config.Rules.FirstOrDefault(r => 
            r.IsEnabled && string.Equals(r.Extension, normalizedExtension, StringComparison.OrdinalIgnoreCase));
        
        _logger.LogDebug("GetRuleForExtension({Extension}): {Result}", 
            normalizedExtension, rule != null ? $"Found -> {rule.Destination}" : "Not found");
        
        return rule;
    }

    /// <inheritdoc />
    public async Task<FileRule> SaveRuleAsync(string extension, string destination, bool autoMove = false, CancellationToken cancellationToken = default)
    {
        var normalizedExtension = NormalizeExtension(extension);
        _logger.LogInformation("Saving rule: {Extension} -> {Destination} (AutoMove: {AutoMove})", 
            normalizedExtension, destination, autoMove);
        
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var config = await GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
            var existingRule = FindRule(config, normalizedExtension);

            if (existingRule != null)
            {
                _logger.LogDebug("Updating existing rule for {Extension}", normalizedExtension);
                existingRule.Destination = destination;
                existingRule.AutoMove = autoMove;
                existingRule.LastUsedAt = DateTime.UtcNow;
            }
            else
            {
                _logger.LogDebug("Creating new rule for {Extension}", normalizedExtension);
                existingRule = new FileRule
                {
                    Extension = normalizedExtension,
                    Destination = destination,
                    AutoMove = autoMove
                };
                config.Rules.Add(existingRule);
            }

            await SaveConfigurationAsync(config, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Rule saved successfully for {Extension}", normalizedExtension);
            return existingRule;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task UpdateRuleAsync(FileRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _logger.LogDebug("Updating rule for {Extension}", rule.Extension);
        
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var config = await GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
            var existingRule = FindRule(config, rule.Extension);

            if (existingRule == null)
            {
                _logger.LogWarning("Rule not found for extension: {Extension}", rule.Extension);
                throw new InvalidOperationException($"Rule for extension '{rule.Extension}' not found.");
            }

            existingRule.Destination = rule.Destination;
            existingRule.AutoMove = rule.AutoMove;
            existingRule.IsEnabled = rule.IsEnabled;

            await SaveConfigurationAsync(config, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Rule updated for {Extension}", rule.Extension);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveRuleAsync(string extension, CancellationToken cancellationToken = default)
    {
        var normalizedExtension = NormalizeExtension(extension);
        _logger.LogInformation("Removing rule for {Extension}", normalizedExtension);
        
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var config = await GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
            var rule = FindRule(config, normalizedExtension);

            if (rule == null)
            {
                _logger.LogWarning("Cannot remove - rule not found: {Extension}", normalizedExtension);
                return false;
            }

            config.Rules.Remove(rule);
            await SaveConfigurationAsync(config, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Rule removed for {Extension}", normalizedExtension);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task UpdateRuleUsageAsync(string extension, CancellationToken cancellationToken = default)
    {
        var normalizedExtension = NormalizeExtension(extension);
        
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var config = await GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
            var rule = FindRule(config, normalizedExtension);

            if (rule != null)
            {
                rule.LastUsedAt = DateTime.UtcNow;
                rule.UseCount++;
                await SaveConfigurationAsync(config, cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Updated usage for {Extension}: Count={Count}", normalizedExtension, rule.UseCount);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetAutoMoveAsync(string extension, bool autoMove, CancellationToken cancellationToken = default)
    {
        var normalizedExtension = NormalizeExtension(extension);
        _logger.LogDebug("Setting AutoMove for {Extension} to {AutoMove}", normalizedExtension, autoMove);
        
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var config = await GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
            var rule = FindRule(config, normalizedExtension);

            if (rule != null)
            {
                rule.AutoMove = autoMove;
                await SaveConfigurationAsync(config, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetRuleEnabledAsync(string extension, bool isEnabled, CancellationToken cancellationToken = default)
    {
        var normalizedExtension = NormalizeExtension(extension);
        _logger.LogDebug("Setting IsEnabled for {Extension} to {IsEnabled}", normalizedExtension, isEnabled);
        
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var config = await GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
            var rule = FindRule(config, normalizedExtension);

            if (rule != null)
            {
                rule.IsEnabled = isEnabled;
                await SaveConfigurationAsync(config, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<int> UpdateRulesDestinationAsync(string oldPath, string newPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(oldPath))
            throw new ArgumentException("Old path cannot be empty.", nameof(oldPath));
        if (string.IsNullOrWhiteSpace(newPath))
            throw new ArgumentException("New path cannot be empty.", nameof(newPath));

        // Normalize paths for comparison
        var normalizedOldPath = Path.GetFullPath(oldPath).TrimEnd(Path.DirectorySeparatorChar);
        var normalizedNewPath = Path.GetFullPath(newPath).TrimEnd(Path.DirectorySeparatorChar);

        _logger.LogInformation("Updating rules destination from '{OldPath}' to '{NewPath}'", normalizedOldPath, normalizedNewPath);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var config = await GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
            var updatedCount = 0;

            foreach (var rule in config.Rules)
            {
                var normalizedDest = Path.GetFullPath(rule.Destination).TrimEnd(Path.DirectorySeparatorChar);
                
                // Check for exact match OR if rule destination starts with old path (handles subfolders)
                if (string.Equals(normalizedDest, normalizedOldPath, StringComparison.OrdinalIgnoreCase))
                {
                    // Exact match - replace entirely
                    _logger.LogDebug("Updating rule {Extension}: {OldDest} -> {NewDest}", 
                        rule.Extension, rule.Destination, normalizedNewPath);
                    rule.Destination = normalizedNewPath;
                    updatedCount++;
                }
                else if (normalizedDest.StartsWith(normalizedOldPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    // Rule destination is inside the old folder - update the base path
                    var relativePart = normalizedDest.Substring(normalizedOldPath.Length);
                    var newDestination = normalizedNewPath + relativePart;
                    _logger.LogDebug("Updating rule {Extension}: {OldDest} -> {NewDest} (subfolder)", 
                        rule.Extension, rule.Destination, newDestination);
                    rule.Destination = newDestination;
                    updatedCount++;
                }
            }

            if (updatedCount > 0)
            {
                await SaveConfigurationAsync(config, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Updated {Count} rules to new destination path", updatedCount);
            }
            else
            {
                _logger.LogDebug("No rules found matching old path '{OldPath}'", normalizedOldPath);
            }

            return updatedCount;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<RulesConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedConfiguration != null)
            return _cachedConfiguration;

        _logger.LogDebug("Loading rules configuration from disk");
        _cachedConfiguration = await _storageService.ReadJsonAsync<RulesConfiguration>(_storageService.RulesFilePath, cancellationToken).ConfigureAwait(false)
                               ?? new RulesConfiguration();
        
        return _cachedConfiguration;
    }

    private async Task SaveConfigurationAsync(RulesConfiguration config, CancellationToken cancellationToken = default)
    {
        await _storageService.WriteJsonAsync(_storageService.RulesFilePath, config, cancellationToken).ConfigureAwait(false);
        _cachedConfiguration = config;
    }

    private static FileRule? FindRule(RulesConfiguration config, string normalizedExtension)
    {
        return config.Rules.FirstOrDefault(r => 
            string.Equals(r.Extension, normalizedExtension, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            throw new ArgumentException("Extension cannot be null or empty.", nameof(extension));

        return extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";
    }

    /// <summary>
    /// Disposes resources used by the service.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        
        _lock.Dispose();
        _disposed = true;
        _logger.LogDebug("RuleService disposed");
    }
}
