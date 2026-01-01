using AutoDrop.Models;
using AutoDrop.Services.Interfaces;

namespace AutoDrop.Services.Implementations;

/// <summary>
/// Implementation of rule service for managing file extension rules.
/// </summary>
public sealed class RuleService : IRuleService
{
    private readonly IStorageService _storageService;
    private RulesConfiguration? _cachedConfiguration;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RuleService(IStorageService storageService)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileRule>> GetAllRulesAsync()
    {
        var config = await GetConfigurationAsync();
        return config.Rules.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<FileRule?> GetRuleForExtensionAsync(string extension)
    {
        var normalizedExtension = NormalizeExtension(extension);
        var config = await GetConfigurationAsync();
        
        return config.Rules.FirstOrDefault(r => 
            string.Equals(r.Extension, normalizedExtension, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<FileRule> SaveRuleAsync(string extension, string destination)
    {
        var normalizedExtension = NormalizeExtension(extension);
        
        await _lock.WaitAsync();
        try
        {
            var config = await GetConfigurationAsync();
            var existingRule = config.Rules.FirstOrDefault(r => 
                string.Equals(r.Extension, normalizedExtension, StringComparison.OrdinalIgnoreCase));

            if (existingRule != null)
            {
                existingRule.Destination = destination;
                existingRule.LastUsedAt = DateTime.UtcNow;
            }
            else
            {
                existingRule = new FileRule
                {
                    Extension = normalizedExtension,
                    Destination = destination
                };
                config.Rules.Add(existingRule);
            }

            await SaveConfigurationAsync(config);
            return existingRule;
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
        
        await _lock.WaitAsync();
        try
        {
            var config = await GetConfigurationAsync();
            var rule = config.Rules.FirstOrDefault(r => 
                string.Equals(r.Extension, normalizedExtension, StringComparison.OrdinalIgnoreCase));

            if (rule == null)
            {
                return false;
            }

            config.Rules.Remove(rule);
            await SaveConfigurationAsync(config);
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
            var rule = config.Rules.FirstOrDefault(r => 
                string.Equals(r.Extension, normalizedExtension, StringComparison.OrdinalIgnoreCase));

            if (rule != null)
            {
                rule.LastUsedAt = DateTime.UtcNow;
                rule.UseCount++;
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
        {
            return _cachedConfiguration;
        }

        _cachedConfiguration = await _storageService.ReadJsonAsync<RulesConfiguration>(_storageService.RulesFilePath)
                               ?? new RulesConfiguration();
        
        return _cachedConfiguration;
    }

    private async Task SaveConfigurationAsync(RulesConfiguration config)
    {
        await _storageService.WriteJsonAsync(_storageService.RulesFilePath, config);
        _cachedConfiguration = config;
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new ArgumentException("Extension cannot be null or empty.", nameof(extension));
        }

        return extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";
    }
}
