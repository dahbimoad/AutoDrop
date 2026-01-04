using AutoDrop.Models;
using AutoDrop.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoDrop.Services.Implementations;

/// <summary>
/// Implementation of rule service for managing file extension rules.
/// </summary>
public sealed class RuleService : IRuleService
{
    private readonly IStorageService _storageService;
    private readonly ILogger<RuleService> _logger;
    private RulesConfiguration? _cachedConfiguration;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RuleService(IStorageService storageService, ILogger<RuleService> logger)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _logger = logger;
        _logger.LogDebug("RuleService initialized");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileRule>> GetAllRulesAsync()
    {
        var config = await GetConfigurationAsync();
        _logger.LogDebug("Retrieved {Count} rules", config.Rules.Count);
        return config.Rules.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<FileRule?> GetRuleForExtensionAsync(string extension)
    {
        var normalizedExtension = NormalizeExtension(extension);
        var config = await GetConfigurationAsync();
        
        var rule = config.Rules.FirstOrDefault(r => 
            r.IsEnabled && string.Equals(r.Extension, normalizedExtension, StringComparison.OrdinalIgnoreCase));
        
        _logger.LogDebug("GetRuleForExtension({Extension}): {Result}", 
            normalizedExtension, rule != null ? $"Found -> {rule.Destination}" : "Not found");
        
        return rule;
    }

    /// <inheritdoc />
    public async Task<FileRule> SaveRuleAsync(string extension, string destination, bool autoMove = false)
    {
        var normalizedExtension = NormalizeExtension(extension);
        _logger.LogInformation("Saving rule: {Extension} -> {Destination} (AutoMove: {AutoMove})", 
            normalizedExtension, destination, autoMove);
        
        await _lock.WaitAsync();
        try
        {
            var config = await GetConfigurationAsync();
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

            await SaveConfigurationAsync(config);
            _logger.LogInformation("Rule saved successfully for {Extension}", normalizedExtension);
            return existingRule;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task UpdateRuleAsync(FileRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _logger.LogDebug("Updating rule for {Extension}", rule.Extension);
        
        await _lock.WaitAsync();
        try
        {
            var config = await GetConfigurationAsync();
            var existingRule = FindRule(config, rule.Extension);

            if (existingRule == null)
            {
                _logger.LogWarning("Rule not found for extension: {Extension}", rule.Extension);
                throw new InvalidOperationException($"Rule for extension '{rule.Extension}' not found.");
            }

            existingRule.Destination = rule.Destination;
            existingRule.AutoMove = rule.AutoMove;
            existingRule.IsEnabled = rule.IsEnabled;

            await SaveConfigurationAsync(config);
            _logger.LogDebug("Rule updated for {Extension}", rule.Extension);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveRuleAsync(string extension)
    {
        var normalizedExtension = NormalizeExtension(extension);
        _logger.LogInformation("Removing rule for {Extension}", normalizedExtension);
        
        await _lock.WaitAsync();
        try
        {
            var config = await GetConfigurationAsync();
            var rule = FindRule(config, normalizedExtension);

            if (rule == null)
            {
                _logger.LogWarning("Cannot remove - rule not found: {Extension}", normalizedExtension);
                return false;
            }

            config.Rules.Remove(rule);
            await SaveConfigurationAsync(config);
            _logger.LogInformation("Rule removed for {Extension}", normalizedExtension);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task UpdateRuleUsageAsync(string extension)
    {
        var normalizedExtension = NormalizeExtension(extension);
        
        await _lock.WaitAsync();
        try
        {
            var config = await GetConfigurationAsync();
            var rule = FindRule(config, normalizedExtension);

            if (rule != null)
            {
                rule.LastUsedAt = DateTime.UtcNow;
                rule.UseCount++;
                await SaveConfigurationAsync(config);
                _logger.LogDebug("Updated usage for {Extension}: Count={Count}", normalizedExtension, rule.UseCount);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetAutoMoveAsync(string extension, bool autoMove)
    {
        var normalizedExtension = NormalizeExtension(extension);
        _logger.LogDebug("Setting AutoMove for {Extension} to {AutoMove}", normalizedExtension, autoMove);
        
        await _lock.WaitAsync();
        try
        {
            var config = await GetConfigurationAsync();
            var rule = FindRule(config, normalizedExtension);

            if (rule != null)
            {
                rule.AutoMove = autoMove;
                await SaveConfigurationAsync(config);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetRuleEnabledAsync(string extension, bool isEnabled)
    {
        var normalizedExtension = NormalizeExtension(extension);
        _logger.LogDebug("Setting IsEnabled for {Extension} to {IsEnabled}", normalizedExtension, isEnabled);
        
        await _lock.WaitAsync();
        try
        {
            var config = await GetConfigurationAsync();
            var rule = FindRule(config, normalizedExtension);

            if (rule != null)
            {
                rule.IsEnabled = isEnabled;
                await SaveConfigurationAsync(config);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<RulesConfiguration> GetConfigurationAsync()
    {
        if (_cachedConfiguration != null)
            return _cachedConfiguration;

        _logger.LogDebug("Loading rules configuration from disk");
        _cachedConfiguration = await _storageService.ReadJsonAsync<RulesConfiguration>(_storageService.RulesFilePath)
                               ?? new RulesConfiguration();
        
        return _cachedConfiguration;
    }

    private async Task SaveConfigurationAsync(RulesConfiguration config)
    {
        await _storageService.WriteJsonAsync(_storageService.RulesFilePath, config);
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
}
