using AutoDrop.Models;
using AutoDrop.Services.AI.Providers;
using AutoDrop.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoDrop.Services.AI;

/// <summary>
/// Main AI service that orchestrates provider selection and file analysis.
/// </summary>
public sealed class AiService : IAiService, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly ICredentialService _credentialService;
    private readonly ILogger<AiService> _logger;
    private readonly Dictionary<AiProvider, IAiProvider> _providers;
    private readonly IReadOnlyList<AiProviderInfo> _availableProviders;
    private IAiProvider? _activeProvider;
    private bool _disposed;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"
    };

    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".json", ".xml", ".csv", ".log", ".pdf"
    };

    public AiService(
        ISettingsService settingsService,
        ICredentialService credentialService,
        ILogger<AiService> logger,
        IEnumerable<IAiProvider> providers)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _credentialService = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(providers);

        // Use injected providers from DI container to avoid duplicate instances
        _providers = providers.ToDictionary(p => p.ProviderType);

        // Order providers with Local first (default), then alphabetically by name
        _availableProviders = _providers.Values
            .OrderByDescending(p => p.ProviderType == AiProvider.Local)
            .ThenBy(p => p.ProviderInfo.DisplayName)
            .Select(p => p.ProviderInfo)
            .ToList();
        _logger.LogDebug("AiService initialized with {Count} providers", _providers.Count);
    }

    private AiSettings? _cachedSettings;
    private DateTime _settingsCacheTime = DateTime.MinValue;
    private static readonly TimeSpan SettingsCacheDuration = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Checks if the cached settings are still valid (not expired).
    /// </summary>
    private bool IsCacheValid => _cachedSettings != null && 
                                  DateTime.UtcNow - _settingsCacheTime < SettingsCacheDuration;

    public AiProvider ActiveProvider => _activeProvider?.ProviderType ?? AiProvider.Local;
    public IReadOnlyList<AiProviderInfo> AvailableProviders => _availableProviders;
    public AiProviderInfo? ActiveProviderInfo => _activeProvider?.ProviderInfo;
    public bool SupportsVision => _activeProvider?.SupportsVision ?? false;
    public bool SupportsPdf => _activeProvider?.SupportsPdf ?? false;
    public bool SupportsTextPrompts => _activeProvider?.ProviderInfo.SupportsTextPrompts ?? false;

    /// <summary>
    /// Checks if AI is available. Uses cached settings to avoid blocking.
    /// Call RefreshAvailabilityAsync() to update the cache if needed.
    /// </summary>
    public bool IsAvailable
    {
        get
        {
            // Use cached settings to avoid blocking UI thread (returns false if cache expired)
            if (!IsCacheValid) return false;
            return _cachedSettings!.Enabled && 
                   _cachedSettings.DisclaimerAccepted && 
                   _activeProvider != null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (!IsCacheValid)
        {
            await RefreshAvailabilityAsync(ct).ConfigureAwait(false);
        }
        
        return _cachedSettings?.Enabled == true && 
               _cachedSettings.DisclaimerAccepted && 
               _activeProvider != null;
    }

    /// <summary>
    /// Refreshes the availability check asynchronously.
    /// </summary>
    public async Task RefreshAvailabilityAsync(CancellationToken ct = default)
    {
        _cachedSettings = await GetAiSettingsAsync();
        _settingsCacheTime = DateTime.UtcNow;
        await InitializeActiveProviderAsync();
    }

    public async Task SetActiveProviderAsync(AiProvider provider, CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(provider, out var newProvider))
        {
            _logger.LogWarning("Provider {Provider} not found", provider);
            return;
        }

        _activeProvider = newProvider;
        
        // Load and apply configuration
        var settings = await GetAiSettingsAsync();
        var config = settings?.ProviderConfigs?.FirstOrDefault(c => c.Provider == provider);
        
        if (config != null)
        {
            _activeProvider.Configure(config);
        }

        // Save active provider to settings
        if (settings != null)
        {
            settings.ActiveProvider = provider;
            await SaveAiSettingsAsync(settings);
        }

        _logger.LogInformation("Active AI provider set to {Provider}", provider);
    }

    public async Task ConfigureProviderAsync(AiProviderConfig config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (!_providers.TryGetValue(config.Provider, out var provider))
        {
            _logger.LogWarning("Provider {Provider} not found", config.Provider);
            return;
        }

        provider.Configure(config);

        // Save configuration
        var settings = await GetAiSettingsAsync() ?? new AiSettings();
        var existingConfig = settings.ProviderConfigs.FirstOrDefault(c => c.Provider == config.Provider);
        
        if (existingConfig != null)
        {
            settings.ProviderConfigs.Remove(existingConfig);
        }
        
        settings.ProviderConfigs.Add(config);
        await SaveAiSettingsAsync(settings);

        _logger.LogInformation("Provider {Provider} configured", config.Provider);
    }

    public async Task<bool> ValidateProviderAsync(AiProvider provider, CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(provider, out var aiProvider))
        {
            _logger.LogWarning("Provider {Provider} not found for validation", provider);
            return false;
        }

        _logger.LogInformation("Validating provider {Provider}", provider);
        var isValid = await aiProvider.ValidateAsync(ct);
        
        // Update config validation status
        var settings = await GetAiSettingsAsync();
        var config = settings?.ProviderConfigs.FirstOrDefault(c => c.Provider == provider);
        
        if (config != null)
        {
            config.IsValidated = isValid;
            config.LastValidated = DateTime.UtcNow;
            await SaveAiSettingsAsync(settings!);
        }

        _logger.LogInformation("Provider {Provider} validation: {Result}", provider, isValid ? "Success" : "Failed");
        return isValid;
    }

    public async Task<AiAnalysisResult> AnalyzeFileAsync(string filePath, IReadOnlyList<CustomFolder>? customFolders = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var settings = await GetAiSettingsAsync();
        if (settings?.Enabled != true || !settings.DisclaimerAccepted)
        {
            return AiAnalysisResult.Failed("AI features are disabled or disclaimer not accepted.");
        }

        // Update cached settings for IsAvailable property
        _cachedSettings = settings;
        _settingsCacheTime = DateTime.UtcNow;

        if (_activeProvider == null)
        {
            await InitializeActiveProviderAsync();
            if (_activeProvider == null)
            {
                return AiAnalysisResult.Failed("No AI provider configured.");
            }
        }

        var extension = Path.GetExtension(filePath);
        var fileName = Path.GetFileName(filePath);

        // CRITICAL: Check file exists before accessing properties
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("[AI] File no longer exists: {FileName}", fileName);
            return AiAnalysisResult.Failed("File no longer exists or was moved.");
        }

        // CRITICAL: Enforce file size limit from user preferences
        var fileInfo = new FileInfo(filePath);
        var fileSizeMb = fileInfo.Length / (1024.0 * 1024.0);
        if (fileSizeMb > settings.MaxFileSizeMb)
        {
            _logger.LogInformation("[AI] File {FileName} ({Size:F1}MB) exceeds max size limit ({Max}MB), skipping",
                fileName, fileSizeMb, settings.MaxFileSizeMb);
            return AiAnalysisResult.Failed($"File size ({fileSizeMb:F1}MB) exceeds the configured limit ({settings.MaxFileSizeMb}MB).");
        }

        _logger.LogInformation("[AI] Analyzing file: {FileName} with {Provider} (CustomFolders: {FolderCount})", 
            fileName, _activeProvider.ProviderType, customFolders?.Count ?? 0);

        // Check if it's an image and if vision analysis is enabled
        if (ImageExtensions.Contains(extension))
        {
            // CRITICAL: Enforce EnableVisionAnalysis user preference
            if (!settings.EnableVisionAnalysis)
            {
                _logger.LogDebug("[AI] Vision analysis disabled by user settings, skipping image");
                return AiAnalysisResult.Failed("Vision analysis is disabled in settings.");
            }
            if (!_activeProvider.SupportsVision)
            {
                return AiAnalysisResult.Failed($"Current provider ({_activeProvider.ProviderType}) doesn't support image analysis.");
            }
            return await AnalyzeImageAsync(filePath, customFolders, ct);
        }

        // Check if it's a document and if document analysis is enabled
        if (DocumentExtensions.Contains(extension))
        {
            // CRITICAL: Enforce EnableDocumentAnalysis user preference
            if (!settings.EnableDocumentAnalysis)
            {
                _logger.LogDebug("[AI] Document analysis disabled by user settings, skipping document");
                return AiAnalysisResult.Failed("Document analysis is disabled in settings.");
            }
            if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase) && !_activeProvider.SupportsPdf)
            {
                return AiAnalysisResult.Failed($"Current provider ({_activeProvider.ProviderType}) doesn't support PDF analysis. Try Claude or Gemini.");
            }
            return await AnalyzeDocumentAsync(filePath, customFolders, ct);
        }

        return AiAnalysisResult.Failed($"File type '{extension}' is not supported for AI analysis.");
    }

    public async Task<AiAnalysisResult> AnalyzeImageAsync(string imagePath, IReadOnlyList<CustomFolder>? customFolders = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        if (_activeProvider == null)
        {
            await InitializeActiveProviderAsync();
        }

        if (_activeProvider == null)
        {
            return AiAnalysisResult.Failed("No AI provider configured.");
        }

        return await _activeProvider.AnalyzeImageAsync(imagePath, customFolders, ct);
    }

    public async Task<AiAnalysisResult> AnalyzeDocumentAsync(string documentPath, IReadOnlyList<CustomFolder>? customFolders = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);

        if (_activeProvider == null)
        {
            await InitializeActiveProviderAsync();
        }

        if (_activeProvider == null)
        {
            return AiAnalysisResult.Failed("No AI provider configured.");
        }

        return await _activeProvider.AnalyzeDocumentAsync(documentPath, customFolders, ct);
    }

    /// <inheritdoc />
    public async Task<string> AnalyzeTextAsync(string prompt, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        if (_activeProvider == null)
        {
            await InitializeActiveProviderAsync();
        }

        if (_activeProvider == null)
        {
            throw new InvalidOperationException("No AI provider configured.");
        }

        return await _activeProvider.SendTextPromptAsync(prompt, ct);
    }

    /// <summary>
    /// Gets provider config. Uses cached settings to avoid blocking.
    /// </summary>
    public AiProviderConfig? GetProviderConfig(AiProvider provider)
    {
        // Use cached settings to avoid blocking
        return _cachedSettings?.ProviderConfigs.FirstOrDefault(c => c.Provider == provider);
    }

    /// <summary>
    /// Gets provider config asynchronously (preferred method).
    /// </summary>
    public async Task<AiProviderConfig?> GetProviderConfigAsync(AiProvider provider, CancellationToken ct = default)
    {
        var settings = await GetAiSettingsAsync();
        return settings?.ProviderConfigs.FirstOrDefault(c => c.Provider == provider);
    }

    /// <summary>
    /// Gets the API key for a provider, decrypting from secure storage if needed.
    /// </summary>
    public async Task<string?> GetApiKeyAsync(AiProvider provider, CancellationToken ct = default)
    {
        var config = await GetProviderConfigAsync(provider, ct);
        if (config == null)
        {
            return null;
        }

        // If key is stored securely, retrieve from credential service
        if (config.IsKeySecured)
        {
            var secureKey = await _credentialService.GetCredentialAsync(config.CredentialKey);
            if (!string.IsNullOrEmpty(secureKey))
            {
                return secureKey;
            }
            
            _logger.LogWarning("Secure API key not found for provider {Provider}, falling back to config", provider);
        }

        // Fallback to plaintext key (for migration or local providers)
        return string.IsNullOrEmpty(config.ApiKey) ? null : config.ApiKey;
    }

    /// <summary>
    /// Stores an API key securely using DPAPI encryption.
    /// </summary>
    public async Task<bool> StoreApiKeySecurelyAsync(AiProvider provider, string apiKey, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        var config = await GetProviderConfigAsync(provider, ct);
        var credentialKey = $"AutoDrop_{provider}_ApiKey";

        // Store in secure credential storage
        var stored = await _credentialService.StoreCredentialAsync(credentialKey, apiKey);
        if (!stored)
        {
            _logger.LogError("Failed to store API key securely for {Provider}", provider);
            return false;
        }

        // Update config to indicate secure storage and clear plaintext key
        var settings = await GetAiSettingsAsync() ?? new AiSettings();
        var existingConfig = settings.ProviderConfigs.FirstOrDefault(c => c.Provider == provider);
        
        if (existingConfig != null)
        {
            existingConfig.IsKeySecured = true;
            existingConfig.ApiKey = string.Empty; // Clear plaintext key
        }
        else
        {
            settings.ProviderConfigs.Add(new AiProviderConfig
            {
                Provider = provider,
                IsKeySecured = true,
                ApiKey = string.Empty
            });
        }

        await SaveAiSettingsAsync(settings);
        _logger.LogInformation("API key stored securely for {Provider}", provider);
        return true;
    }

    private async Task InitializeActiveProviderAsync()
    {
        var settings = await GetAiSettingsAsync();
        if (settings == null) return;

        var activeProviderType = settings.ActiveProvider;
        
        if (_providers.TryGetValue(activeProviderType, out var provider))
        {
            var config = settings.ProviderConfigs.FirstOrDefault(c => c.Provider == activeProviderType);
            if (config != null)
            {
                // If the API key is stored securely, retrieve it and update the config object
                // before passing to the provider (the config in memory will have the key)
                if (config.IsKeySecured)
                {
                    var secureKey = await _credentialService.GetCredentialAsync(config.CredentialKey);
                    if (!string.IsNullOrEmpty(secureKey))
                    {
                        // Create a copy of config with the decrypted key for provider configuration
                        var configWithKey = new AiProviderConfig
                        {
                            Provider = config.Provider,
                            ApiKey = secureKey,
                            BaseUrl = config.BaseUrl,
                            TextModel = config.TextModel,
                            VisionModel = config.VisionModel,
                            IsValidated = config.IsValidated,
                            LastValidated = config.LastValidated,
                            IsKeySecured = config.IsKeySecured
                        };
                        provider.Configure(configWithKey);
                    }
                }
                else
                {
                    provider.Configure(config);
                }
            }
            _activeProvider = provider;
            _logger.LogDebug("Initialized active provider: {Provider}", activeProviderType);
        }
    }

    private async Task<AiSettings?> GetAiSettingsAsync()
    {
        var appSettings = await _settingsService.GetSettingsAsync();
        return appSettings.AiSettings;
    }

    private async Task SaveAiSettingsAsync(AiSettings aiSettings)
    {
        var appSettings = await _settingsService.GetSettingsAsync();
        appSettings.AiSettings = aiSettings;
        await _settingsService.SaveSettingsAsync(appSettings);
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        // Note: Providers are managed by DI container (singleton lifetime),
        // so we don't dispose them here - the DI container handles their lifecycle.
        _providers.Clear();
        _disposed = true;
        _logger.LogDebug("AiService disposed");
    }
}
