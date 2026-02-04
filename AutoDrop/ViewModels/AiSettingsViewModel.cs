using System.Collections.ObjectModel;
using AutoDrop.Models;
using AutoDrop.Services.AI.Local;
using AutoDrop.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AutoDrop.ViewModels;

/// <summary>
/// ViewModel for the multi-provider AI Settings configuration.
/// </summary>
public sealed partial class AiSettingsViewModel : ObservableObject
{
    private readonly IAiService _aiService;
    private readonly ISettingsService _settingsService;
    private readonly OnnxModelManager _modelManager;
    private readonly ILogger<AiSettingsViewModel> _logger;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEnableAi))]
    private bool _isEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEnableAi))]
    [NotifyPropertyChangedFor(nameof(ShowPrivacyNotice))]
    [NotifyPropertyChangedFor(nameof(ShowAcceptedBadge))]
    [NotifyPropertyChangedFor(nameof(HasVisionModels))]
    private bool _disclaimerAccepted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEnableVision))]
    [NotifyPropertyChangedFor(nameof(CanEnableDocuments))]
    [NotifyPropertyChangedFor(nameof(CurrentModelSupportsVision))]
    [NotifyPropertyChangedFor(nameof(CurrentModelSupportsPdf))]
    [NotifyPropertyChangedFor(nameof(HasVisionModels))]
    private AiProviderInfo? _selectedProvider;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEnableVision))]
    [NotifyPropertyChangedFor(nameof(CurrentModelSupportsVision))]
    private AiModelInfo? _selectedTextModel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEnableVision))]
    [NotifyPropertyChangedFor(nameof(CanEnableDocuments))]
    [NotifyPropertyChangedFor(nameof(CurrentModelSupportsVision))]
    [NotifyPropertyChangedFor(nameof(CurrentModelSupportsPdf))]
    private AiModelInfo? _selectedVisionModel;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _baseUrl = string.Empty;

    [ObservableProperty]
    private bool _enableVisionAnalysis = true;

    [ObservableProperty]
    private bool _enableDocumentAnalysis = true;

    [ObservableProperty]
    private bool _enableSmartRename = true;

    [ObservableProperty]
    private double _confidenceThreshold = 0.7;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaxFileSizeWarning))]
    private int _maxFileSizeMb = 10;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DefaultFolderDisplayPath))]
    private string _defaultNewFolderBasePath = string.Empty;

    [ObservableProperty]
    private bool _isValidating;

    [ObservableProperty]
    private string _validationStatus = string.Empty;

    [ObservableProperty]
    private bool _isApiKeyValid;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private bool _isLoading;

    // Local AI model properties
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LocalModelStatusText))]
    [NotifyPropertyChangedFor(nameof(LocalModelsReady))]
    [NotifyPropertyChangedFor(nameof(CanDownloadModels))]
    private bool _isDownloadingModels;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LocalModelStatusText))]
    [NotifyPropertyChangedFor(nameof(LocalModelsReady))]
    [NotifyPropertyChangedFor(nameof(CanDownloadModels))]
    private bool _localModelsDownloaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LocalModelStatusText))]
    private double _modelDownloadProgress;

    [ObservableProperty]
    private string _modelDownloadMessage = string.Empty;

    public ObservableCollection<AiProviderInfo> AvailableProviders { get; } = [];
    public ObservableCollection<AiModelInfo> TextModels { get; } = [];
    public ObservableCollection<AiModelInfo> VisionModels { get; } = [];

    // Computed properties for UI binding
    public bool RequiresApiKey => SelectedProvider?.RequiresApiKey ?? true;
    public bool IsLocalProvider => SelectedProvider?.IsLocal ?? false;
    public bool HasVisionModels => VisionModels.Count > 0 && ShowAcceptedBadge;
    public string ApiKeyLabel => SelectedProvider?.DisplayName + " API Key" ?? "API Key";
    public string ApiKeyHelpUrl => SelectedProvider?.ApiKeyUrl ?? "";
    
    // Local AI computed properties
    public bool LocalModelsReady => LocalModelsDownloaded && !IsDownloadingModels;
    public bool CanDownloadModels => !IsDownloadingModels && !LocalModelsDownloaded;
    public string LocalModelStatusText => IsDownloadingModels 
        ? $"Downloading... {ModelDownloadProgress:P0}" 
        : LocalModelsDownloaded 
            ? "✓ Models ready (~90MB)" 
            : "Models not downloaded";

    /// <summary>
    /// User must accept privacy terms before enabling AI.
    /// </summary>
    public bool CanEnableAi => DisclaimerAccepted;
    
    /// <summary>
    /// Show privacy notice only if not yet accepted.
    /// </summary>
    public bool ShowPrivacyNotice => !DisclaimerAccepted;
    
    /// <summary>
    /// Show the accepted badge when terms are accepted.
    /// </summary>
    public bool ShowAcceptedBadge => DisclaimerAccepted;
    
    /// <summary>
    /// Vision analysis can only be enabled if selected model supports it.
    /// </summary>
    public bool CanEnableVision => CurrentModelSupportsVision;
    
    /// <summary>
    /// Document analysis can be enabled for all models (text), but PDF requires support.
    /// </summary>
    public bool CanEnableDocuments => true; // All models support text documents
    
    /// <summary>
    /// Whether the currently selected vision model supports vision.
    /// </summary>
    public bool CurrentModelSupportsVision => SelectedVisionModel?.SupportsVision ?? (VisionModels.Count > 0);
    
    /// <summary>
    /// Whether the currently selected model supports PDF analysis.
    /// </summary>
    public bool CurrentModelSupportsPdf => SelectedVisionModel?.SupportsPdf ?? SelectedTextModel?.SupportsPdf ?? false;
    
    /// <summary>
    /// Warning message for max file size.
    /// </summary>
    public string MaxFileSizeWarning => MaxFileSizeMb > 25 
        ? "⚠️ Large files may take longer to analyze" 
        : MaxFileSizeMb < 5 
            ? "⚠️ Small limit may skip many files" 
            : string.Empty;

    /// <summary>
    /// Display path for the default new folder location.
    /// </summary>
    public string DefaultFolderDisplayPath => string.IsNullOrWhiteSpace(DefaultNewFolderBasePath) 
        ? $"📁 Documents ({Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)})"
        : $"📁 {DefaultNewFolderBasePath}";

    public AiSettingsViewModel(
        IAiService aiService,
        ISettingsService settingsService,
        OnnxModelManager modelManager,
        ILogger<AiSettingsViewModel> logger)
    {
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _modelManager = modelManager ?? throw new ArgumentNullException(nameof(modelManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        foreach (var provider in _aiService.AvailableProviders)
        {
            AvailableProviders.Add(provider);
        }

        // Check initial model status
        LocalModelsDownloaded = _modelManager.AreModelsDownloaded();

        _logger.LogDebug("AiSettingsViewModel initialized with {Count} providers, LocalModels={Downloaded}", 
            AvailableProviders.Count, LocalModelsDownloaded);
    }

    public async Task LoadSettingsAsync()
    {
        IsLoading = true;
        try
        {
            var appSettings = await _settingsService.GetSettingsAsync();
            var aiSettings = appSettings.AiSettings;

            IsEnabled = aiSettings.Enabled;
            DisclaimerAccepted = aiSettings.DisclaimerAccepted;
            ConfidenceThreshold = aiSettings.ConfidenceThreshold;
            MaxFileSizeMb = aiSettings.MaxFileSizeMb;
            EnableVisionAnalysis = aiSettings.EnableVisionAnalysis;
            EnableDocumentAnalysis = aiSettings.EnableDocumentAnalysis;
            EnableSmartRename = aiSettings.EnableSmartRename;
            DefaultNewFolderBasePath = aiSettings.DefaultNewFolderBasePath;

            // Set active provider
            SelectedProvider = AvailableProviders.FirstOrDefault(p => p.Provider == aiSettings.ActiveProvider)
                             ?? AvailableProviders.FirstOrDefault();

            if (SelectedProvider != null)
            {
                await LoadProviderConfigAsync(SelectedProvider.Provider);
            }

            HasUnsavedChanges = false;
            _logger.LogDebug("AI settings loaded");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load AI settings");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadProviderConfigAsync(AiProvider provider)
    {
        // Use async method to ensure we get fresh settings from storage, not stale cache
        var config = await _aiService.GetProviderConfigAsync(provider);
        
        // Get API key from secure storage if available
        if (config?.IsKeySecured == true)
        {
            var secureKey = await _aiService.GetApiKeyAsync(provider);
            ApiKey = secureKey ?? string.Empty;
        }
        else
        {
            ApiKey = config?.ApiKey ?? string.Empty;
        }
        
        BaseUrl = config?.BaseUrl ?? SelectedProvider?.DefaultBaseUrl ?? string.Empty;
        IsApiKeyValid = config?.IsValidated ?? false;
        ValidationStatus = config?.IsValidated == true ? "✓ Validated" : string.Empty;

        UpdateModelLists();

        // Select saved models or defaults
        if (config != null)
        {
            SelectedTextModel = TextModels.FirstOrDefault(m => m.Id == config.TextModel) ?? TextModels.FirstOrDefault();
            SelectedVisionModel = VisionModels.FirstOrDefault(m => m.Id == config.VisionModel) ?? VisionModels.FirstOrDefault();
        }
        else
        {
            SelectedTextModel = TextModels.FirstOrDefault();
            SelectedVisionModel = VisionModels.FirstOrDefault();
        }
    }

    private void UpdateModelLists()
    {
        TextModels.Clear();
        VisionModels.Clear();

        if (SelectedProvider == null) return;

        foreach (var model in SelectedProvider.Models)
        {
            TextModels.Add(model);
            if (model.SupportsVision)
            {
                VisionModels.Add(model);
            }
        }

        OnPropertyChanged(nameof(HasVisionModels));
    }

    partial void OnSelectedProviderChanged(AiProviderInfo? value)
    {
        if (value == null) return;

        HasUnsavedChanges = true;
        IsApiKeyValid = false;
        ValidationStatus = string.Empty;
        ApiKey = string.Empty;
        BaseUrl = value.DefaultBaseUrl ?? string.Empty;

        UpdateModelLists();
        SelectedTextModel = TextModels.FirstOrDefault();
        SelectedVisionModel = VisionModels.FirstOrDefault();

        OnPropertyChanged(nameof(RequiresApiKey));
        OnPropertyChanged(nameof(IsLocalProvider));
        OnPropertyChanged(nameof(ApiKeyLabel));
        OnPropertyChanged(nameof(ApiKeyHelpUrl));

        // Load existing config if available (fire async task with error handling)
        _ = LoadProviderConfigSafeAsync(value.Provider);
    }

    /// <summary>
    /// Safely loads provider config with error handling for partial method context.
    /// </summary>
    private async Task LoadProviderConfigSafeAsync(AiProvider provider)
    {
        try
        {
            await LoadProviderConfigAsync(provider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load provider config for {Provider}", provider);
        }
    }

    partial void OnApiKeyChanged(string value)
    {
        HasUnsavedChanges = true;
        IsApiKeyValid = false;
        ValidationStatus = string.Empty;
    }

    partial void OnIsEnabledChanged(bool value) => HasUnsavedChanges = true;
    
    partial void OnEnableVisionAnalysisChanged(bool value)
    {
        HasUnsavedChanges = true;
        // Auto-disable if model doesn't support vision
        if (value && !CanEnableVision)
        {
            EnableVisionAnalysis = false;
        }
    }
    
    partial void OnEnableDocumentAnalysisChanged(bool value) => HasUnsavedChanges = true;
    partial void OnEnableSmartRenameChanged(bool value) => HasUnsavedChanges = true;
    partial void OnConfidenceThresholdChanged(double value) => HasUnsavedChanges = true;
    partial void OnMaxFileSizeMbChanged(int value) 
    {
        HasUnsavedChanges = true;
        OnPropertyChanged(nameof(MaxFileSizeWarning));
    }
    partial void OnDefaultNewFolderBasePathChanged(string value)
    {
        HasUnsavedChanges = true;
        OnPropertyChanged(nameof(DefaultFolderDisplayPath));
    }
    partial void OnSelectedTextModelChanged(AiModelInfo? value)
    {
        HasUnsavedChanges = true;
        // Update feature availability
        OnPropertyChanged(nameof(CanEnableVision));
        OnPropertyChanged(nameof(CurrentModelSupportsVision));
        OnPropertyChanged(nameof(CurrentModelSupportsPdf));
    }
    partial void OnSelectedVisionModelChanged(AiModelInfo? value)
    {
        HasUnsavedChanges = true;
        // Update feature availability
        OnPropertyChanged(nameof(CanEnableVision));
        OnPropertyChanged(nameof(CurrentModelSupportsVision));
        OnPropertyChanged(nameof(CurrentModelSupportsPdf));
        
        // Auto-disable vision if model doesn't support it
        if (value != null && !value.SupportsVision && EnableVisionAnalysis)
        {
            EnableVisionAnalysis = false;
        }
    }

    [RelayCommand]
    private void AcceptDisclaimer()
    {
        DisclaimerAccepted = true;
        IsEnabled = true; // Auto-enable AI when accepting terms
        HasUnsavedChanges = true;
        _logger.LogInformation("Privacy terms accepted, AI features enabled");
    }

    [RelayCommand]
    private async Task ValidateApiKeyAsync()
    {
        if (SelectedProvider == null) return;

        // For local providers, validate connection
        if (!RequiresApiKey)
        {
            await ValidateLocalProviderAsync();
            return;
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            ValidationStatus = "Please enter an API key first.";
            IsApiKeyValid = false;
            return;
        }

        IsValidating = true;
        ValidationStatus = "Validating...";

        try
        {
            // Configure and validate
            var config = CreateCurrentConfig();
            await _aiService.ConfigureProviderAsync(config);
            var isValid = await _aiService.ValidateProviderAsync(SelectedProvider.Provider);

            IsApiKeyValid = isValid;
            ValidationStatus = isValid 
                ? "✓ API key is valid!" 
                : "✗ Invalid API key. Please check and try again.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API key validation failed");
            ValidationStatus = $"✗ Validation error: {ex.Message}";
            IsApiKeyValid = false;
        }
        finally
        {
            IsValidating = false;
        }
    }

    private async Task ValidateLocalProviderAsync()
    {
        IsValidating = true;
        ValidationStatus = "Checking Local AI models...";

        try
        {
            var config = CreateCurrentConfig();
            await _aiService.ConfigureProviderAsync(config);
            var isValid = await _aiService.ValidateProviderAsync(AiProvider.Local);

            IsApiKeyValid = isValid;
            ValidationStatus = isValid 
                ? "✓ Local AI models ready!" 
                : "✗ Models not downloaded. Click 'Download Models' to set up.";
                
            // Update local models status
            LocalModelsDownloaded = isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local AI validation failed");
            ValidationStatus = $"✗ Validation error: {ex.Message}";
            IsApiKeyValid = false;
        }
        finally
        {
            IsValidating = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDownloadModels))]
    private async Task DownloadModelsAsync(CancellationToken ct)
    {
        IsDownloadingModels = true;
        ModelDownloadProgress = 0;
        ModelDownloadMessage = "Starting download...";
        ValidationStatus = "Downloading AI models...";

        try
        {
            var progress = new Progress<LocalModelStatus>(status =>
            {
                ModelDownloadProgress = status.DownloadProgress;
                ModelDownloadMessage = status.StatusMessage;
                
                if (status.IsReady)
                {
                    LocalModelsDownloaded = true;
                    ValidationStatus = "✓ Models downloaded successfully!";
                    IsApiKeyValid = true;
                }
                else if (status.HasError)
                {
                    ValidationStatus = $"✗ Download failed: {status.ErrorMessage}";
                }
            });

            await _modelManager.DownloadModelsAsync(progress, ct);
            
            // Verify download
            LocalModelsDownloaded = _modelManager.AreModelsDownloaded();
            
            if (LocalModelsDownloaded)
            {
                _logger.LogInformation("Local AI models downloaded successfully");
                ValidationStatus = "✓ Models ready! Local AI is now available.";
                IsApiKeyValid = true;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Model download cancelled by user");
            ValidationStatus = "Download cancelled.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download AI models");
            ValidationStatus = $"✗ Download failed: {ex.Message}";
        }
        finally
        {
            IsDownloadingModels = false;
            OnPropertyChanged(nameof(CanDownloadModels));
            OnPropertyChanged(nameof(LocalModelsReady));
            OnPropertyChanged(nameof(LocalModelStatusText));
        }
    }

    [RelayCommand]
    private void BrowseDefaultFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Default Location for AI-Suggested Folders",
            InitialDirectory = string.IsNullOrWhiteSpace(DefaultNewFolderBasePath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : DefaultNewFolderBasePath
        };

        if (dialog.ShowDialog() == true)
        {
            DefaultNewFolderBasePath = dialog.FolderName;
            _logger.LogInformation("Default new folder path set to: {Path}", dialog.FolderName);
        }
    }

    [RelayCommand]
    private void ResetDefaultFolder()
    {
        DefaultNewFolderBasePath = string.Empty;
        _logger.LogInformation("Default new folder path reset to Documents");
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        if (SelectedProvider == null) return;

        try
        {
            // Store API key securely if provided (for cloud providers)
            if (!string.IsNullOrEmpty(ApiKey) && SelectedProvider.RequiresApiKey)
            {
                var stored = await _aiService.StoreApiKeySecurelyAsync(SelectedProvider.Provider, ApiKey);
                if (!stored)
                {
                    _logger.LogWarning("Failed to store API key securely, falling back to config storage");
                }
            }

            // Save provider configuration (with secure key flag if applicable)
            var config = CreateCurrentConfig();
            await _aiService.ConfigureProviderAsync(config);
            await _aiService.SetActiveProviderAsync(SelectedProvider.Provider);

            // Save general AI settings
            var appSettings = await _settingsService.GetSettingsAsync();
            appSettings.AiSettings.Enabled = IsEnabled;
            appSettings.AiSettings.DisclaimerAccepted = DisclaimerAccepted;
            appSettings.AiSettings.ConfidenceThreshold = ConfidenceThreshold;
            appSettings.AiSettings.MaxFileSizeMb = MaxFileSizeMb;
            appSettings.AiSettings.EnableVisionAnalysis = EnableVisionAnalysis;
            appSettings.AiSettings.EnableDocumentAnalysis = EnableDocumentAnalysis;
            appSettings.AiSettings.EnableSmartRename = EnableSmartRename;
            appSettings.AiSettings.ActiveProvider = SelectedProvider.Provider;
            appSettings.AiSettings.DefaultNewFolderBasePath = DefaultNewFolderBasePath;

            await _settingsService.SaveSettingsAsync(appSettings);

            HasUnsavedChanges = false;
            _logger.LogInformation("AI settings saved for provider {Provider}", SelectedProvider.Provider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save AI settings");
        }
    }

    private AiProviderConfig CreateCurrentConfig() => new()
    {
        Provider = SelectedProvider?.Provider ?? AiProvider.Local,
        ApiKey = string.Empty, // No longer store API key in config (use secure storage)
        IsKeySecured = !string.IsNullOrEmpty(ApiKey) && (SelectedProvider?.RequiresApiKey ?? false),
        BaseUrl = string.IsNullOrWhiteSpace(BaseUrl) ? null : BaseUrl,
        TextModel = SelectedTextModel?.Id ?? string.Empty,
        VisionModel = SelectedVisionModel?.Id ?? string.Empty,
        IsValidated = IsApiKeyValid,
        LastValidated = IsApiKeyValid ? DateTime.UtcNow : null
    };
}
