using AutoDrop.Models;
using AutoDrop.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AutoDrop.ViewModels;

/// <summary>
/// ViewModel for AI Settings configuration.
/// </summary>
public sealed partial class AiSettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IGeminiService _geminiService;
    private readonly ILogger<AiSettingsViewModel> _logger;
    private AiSettings _aiSettings = new();

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private bool _disclaimerAccepted;

    [ObservableProperty]
    private bool _enableVisionAnalysis = true;

    [ObservableProperty]
    private bool _enableDocumentAnalysis = true;

    [ObservableProperty]
    private bool _enableSmartRename = true;

    [ObservableProperty]
    private double _confidenceThreshold = 0.7;

    [ObservableProperty]
    private int _maxFileSizeMb = 10;

    [ObservableProperty]
    private bool _isValidating;

    [ObservableProperty]
    private string _validationStatus = string.Empty;

    [ObservableProperty]
    private bool _isApiKeyValid;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    public AiSettingsViewModel(
        ISettingsService settingsService,
        IGeminiService geminiService,
        ILogger<AiSettingsViewModel> logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _geminiService = geminiService ?? throw new ArgumentNullException(nameof(geminiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("AiSettingsViewModel initialized");
    }

    /// <summary>
    /// Loads settings from storage.
    /// </summary>
    public async Task LoadSettingsAsync()
    {
        try
        {
            var appSettings = await _settingsService.GetSettingsAsync().ConfigureAwait(false);
            _aiSettings = appSettings.AiSettings;

            ApiKey = _aiSettings.ApiKey;
            IsEnabled = _aiSettings.Enabled;
            DisclaimerAccepted = _aiSettings.DisclaimerAccepted;
            EnableVisionAnalysis = _aiSettings.EnableVisionAnalysis;
            EnableDocumentAnalysis = _aiSettings.EnableDocumentAnalysis;
            EnableSmartRename = _aiSettings.EnableSmartRename;
            ConfidenceThreshold = _aiSettings.ConfidenceThreshold;
            MaxFileSizeMb = _aiSettings.MaxFileSizeMb;

            HasUnsavedChanges = false;
            _logger.LogDebug("AI settings loaded");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load AI settings");
        }
    }

    /// <summary>
    /// Saves settings to storage.
    /// </summary>
    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            var appSettings = await _settingsService.GetSettingsAsync().ConfigureAwait(false);
            
            appSettings.AiSettings.ApiKey = ApiKey;
            appSettings.AiSettings.Enabled = IsEnabled;
            appSettings.AiSettings.DisclaimerAccepted = DisclaimerAccepted;
            appSettings.AiSettings.EnableVisionAnalysis = EnableVisionAnalysis;
            appSettings.AiSettings.EnableDocumentAnalysis = EnableDocumentAnalysis;
            appSettings.AiSettings.EnableSmartRename = EnableSmartRename;
            appSettings.AiSettings.ConfidenceThreshold = ConfidenceThreshold;
            appSettings.AiSettings.MaxFileSizeMb = MaxFileSizeMb;

            await _settingsService.SaveSettingsAsync(appSettings).ConfigureAwait(false);
            
            HasUnsavedChanges = false;
            _logger.LogInformation("AI settings saved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save AI settings");
        }
    }

    /// <summary>
    /// Validates the API key with Google's servers.
    /// </summary>
    [RelayCommand]
    private async Task ValidateApiKeyAsync()
    {
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
            // Temporarily save the key
            var appSettings = await _settingsService.GetSettingsAsync().ConfigureAwait(false);
            var originalKey = appSettings.AiSettings.ApiKey;
            appSettings.AiSettings.ApiKey = ApiKey;
            await _settingsService.SaveSettingsAsync(appSettings).ConfigureAwait(false);

            var isValid = await _geminiService.ValidateApiKeyAsync().ConfigureAwait(false);
            
            IsApiKeyValid = isValid;
            ValidationStatus = isValid ? "✓ API key is valid!" : "✗ Invalid API key. Please check and try again.";

            if (!isValid)
            {
                // Restore original key if validation failed
                appSettings.AiSettings.ApiKey = originalKey;
                await _settingsService.SaveSettingsAsync(appSettings).ConfigureAwait(false);
            }
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

    /// <summary>
    /// Accepts the AI disclaimer.
    /// </summary>
    [RelayCommand]
    private void AcceptDisclaimer()
    {
        DisclaimerAccepted = true;
        HasUnsavedChanges = true;
    }

    partial void OnApiKeyChanged(string value)
    {
        HasUnsavedChanges = true;
        IsApiKeyValid = false;
        ValidationStatus = string.Empty;
    }

    partial void OnIsEnabledChanged(bool value)
    {
        HasUnsavedChanges = true;
    }

    partial void OnEnableVisionAnalysisChanged(bool value)
    {
        HasUnsavedChanges = true;
    }

    partial void OnEnableDocumentAnalysisChanged(bool value)
    {
        HasUnsavedChanges = true;
    }

    partial void OnEnableSmartRenameChanged(bool value)
    {
        HasUnsavedChanges = true;
    }

    partial void OnConfidenceThresholdChanged(double value)
    {
        HasUnsavedChanges = true;
    }

    partial void OnMaxFileSizeMbChanged(int value)
    {
        HasUnsavedChanges = true;
    }
}
