using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using AutoDrop.Models;
using AutoDrop.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoDrop.ViewModels;

/// <summary>
/// ViewModel for the Rules Management window.
/// Handles rule and custom folder management with full CRUD operations.
/// </summary>
public partial class RulesManagerViewModel : Base.ViewModelBase
{
    private readonly IRuleService _ruleService;
    private readonly ISettingsService _settingsService;
    private readonly INotificationService _notificationService;

    #region Observable Properties

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private FileRule? _selectedRule;

    [ObservableProperty]
    private CustomFolder? _selectedFolder;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _isAddingRule;

    [ObservableProperty]
    private bool _isAddingFolder;

    [ObservableProperty]
    private string _newExtension = string.Empty;

    [ObservableProperty]
    private string _newDestination = string.Empty;

    [ObservableProperty]
    private bool _newAutoMove;

    [ObservableProperty]
    private string _newFolderName = string.Empty;

    [ObservableProperty]
    private string _newFolderPath = string.Empty;

    [ObservableProperty]
    private bool _newFolderPinned;

    [ObservableProperty]
    private string _newFolderIcon = "📁";

    [ObservableProperty]
    private bool _createNewFolder = true; // If true, creates subfolder. If false, links existing folder.

    #endregion

    #region Collections

    public ObservableCollection<FileRule> Rules { get; } = [];
    public ObservableCollection<CustomFolder> CustomFolders { get; } = [];
    public ICollectionView FilteredRules { get; }

    /// <summary>
    /// Available icons for custom folders.
    /// </summary>
    public IReadOnlyList<string> AvailableIcons { get; } = new[]
    {
        "📁", "📂", "💼", "🏠", "🎵", "🎬", "📷", "📄", "📦", "🎮", 
        "💾", "📊", "📈", "🔧", "⚙️", "🚀", "💡", "📚", "🎨", "✉️"
    };

    #endregion

    public RulesManagerViewModel(
        IRuleService ruleService,
        ISettingsService settingsService,
        INotificationService notificationService)
    {
        _ruleService = ruleService ?? throw new ArgumentNullException(nameof(ruleService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));

        // Setup filtered collection view for rules
        FilteredRules = CollectionViewSource.GetDefaultView(Rules);
        FilteredRules.Filter = FilterRules;
    }

    #region Initialization

    /// <summary>
    /// Loads all rules and custom folders.
    /// </summary>
    [RelayCommand]
    public async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            await Task.WhenAll(LoadRulesAsync(), LoadCustomFoldersAsync());
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadRulesAsync()
    {
        var rules = await _ruleService.GetAllRulesAsync();
        Rules.Clear();
        foreach (var rule in rules.OrderBy(r => r.Extension))
        {
            Rules.Add(rule);
        }
    }

    private async Task LoadCustomFoldersAsync()
    {
        var folders = await _settingsService.GetCustomFoldersAsync();
        CustomFolders.Clear();
        foreach (var folder in folders.OrderBy(f => f.Name))
        {
            CustomFolders.Add(folder);
        }
    }

    #endregion

    #region Search & Filter

    partial void OnSearchTextChanged(string value)
    {
        FilteredRules.Refresh();
    }

    private bool FilterRules(object obj)
    {
        if (obj is not FileRule rule)
            return false;

        if (string.IsNullOrWhiteSpace(SearchText))
            return true;

        return rule.Extension.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
               rule.Destination.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Rule Commands

    /// <summary>
    /// Shows the add rule panel.
    /// </summary>
    [RelayCommand]
    private void ShowAddRule()
    {
        ResetNewRuleFields();
        IsAddingRule = true;
    }

    /// <summary>
    /// Cancels adding a new rule.
    /// </summary>
    [RelayCommand]
    private void CancelAddRule()
    {
        IsAddingRule = false;
        ResetNewRuleFields();
    }

    /// <summary>
    /// Saves a new rule.
    /// </summary>
    [RelayCommand]
    private async Task SaveNewRuleAsync()
    {
        if (string.IsNullOrWhiteSpace(NewExtension))
        {
            _notificationService.ShowError("Validation Error", "Extension is required.");
            return;
        }

        if (string.IsNullOrWhiteSpace(NewDestination))
        {
            _notificationService.ShowError("Validation Error", "Destination folder is required.");
            return;
        }

        if (!Directory.Exists(NewDestination))
        {
            _notificationService.ShowError("Validation Error", "Destination folder does not exist.");
            return;
        }

        IsBusy = true;
        try
        {
            var rule = await _ruleService.SaveRuleAsync(NewExtension, NewDestination, NewAutoMove);
            
            // Refresh the list
            await LoadRulesAsync();
            
            IsAddingRule = false;
            ResetNewRuleFields();
            
            _notificationService.ShowSuccess("Rule Created", $"Rule for {rule.Extension} created successfully.");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Error", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Opens folder browser for new rule destination.
    /// </summary>
    [RelayCommand]
    private void BrowseNewRuleDestination()
    {
        var path = ShowFolderBrowserDialog();
        if (!string.IsNullOrEmpty(path))
        {
            NewDestination = path;
        }
    }

    /// <summary>
    /// Toggles auto-move for the selected rule.
    /// </summary>
    [RelayCommand]
    private async Task ToggleAutoMoveAsync(FileRule? rule)
    {
        if (rule == null) return;

        try
        {
            await _ruleService.SetAutoMoveAsync(rule.Extension, !rule.AutoMove);
            rule.AutoMove = !rule.AutoMove;
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Error", ex.Message);
        }
    }

    /// <summary>
    /// Toggles enabled state for the selected rule.
    /// </summary>
    [RelayCommand]
    private async Task ToggleRuleEnabledAsync(FileRule? rule)
    {
        if (rule == null) return;

        try
        {
            await _ruleService.SetRuleEnabledAsync(rule.Extension, !rule.IsEnabled);
            rule.IsEnabled = !rule.IsEnabled;
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Error", ex.Message);
        }
    }

    /// <summary>
    /// Edits the destination for a rule.
    /// </summary>
    [RelayCommand]
    private async Task EditRuleDestinationAsync(FileRule? rule)
    {
        if (rule == null) return;

        var newPath = ShowFolderBrowserDialog(rule.Destination);
        if (string.IsNullOrEmpty(newPath) || newPath == rule.Destination)
            return;

        IsBusy = true;
        try
        {
            rule.Destination = newPath;
            await _ruleService.UpdateRuleAsync(rule);
            FilteredRules.Refresh();
            _notificationService.ShowSuccess("Rule Updated", $"Destination for {rule.Extension} updated.");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Error", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Deletes the selected rule.
    /// </summary>
    [RelayCommand]
    private async Task DeleteRuleAsync(FileRule? rule)
    {
        if (rule == null) return;

        IsBusy = true;
        try
        {
            var removed = await _ruleService.RemoveRuleAsync(rule.Extension);
            if (removed)
            {
                Rules.Remove(rule);
                SelectedRule = null;
                _notificationService.ShowSuccess("Rule Deleted", $"Rule for {rule.Extension} deleted.");
            }
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Error", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ResetNewRuleFields()
    {
        NewExtension = string.Empty;
        NewDestination = string.Empty;
        NewAutoMove = false;
    }

    #endregion

    #region Custom Folder Commands

    /// <summary>
    /// Shows the add folder panel.
    /// </summary>
    [RelayCommand]
    private void ShowAddFolder()
    {
        ResetNewFolderFields();
        IsAddingFolder = true;
    }

    /// <summary>
    /// Cancels adding a new folder.
    /// </summary>
    [RelayCommand]
    private void CancelAddFolder()
    {
        IsAddingFolder = false;
        ResetNewFolderFields();
    }

    /// <summary>
    /// Saves a new custom folder.
    /// </summary>
    [RelayCommand]
    private async Task SaveNewFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(NewFolderName))
        {
            _notificationService.ShowError("Validation Error", "Folder name is required.");
            return;
        }

        if (string.IsNullOrWhiteSpace(NewFolderPath))
        {
            _notificationService.ShowError("Validation Error", "Parent folder path is required.");
            return;
        }

        // Validate path format
        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(NewFolderPath);
        }
        catch
        {
            _notificationService.ShowError("Validation Error", "Invalid folder path format.");
            return;
        }

        // For creating new folders, parent must exist
        if (CreateNewFolder && !Directory.Exists(normalizedPath))
        {
            _notificationService.ShowError("Validation Error", "Parent folder does not exist. Please browse to select a valid folder.");
            return;
        }

        IsBusy = true;
        try
        {
            // AddCustomFolderAsync will create the subfolder physically
            var folder = await _settingsService.AddCustomFolderAsync(
                NewFolderName, 
                normalizedPath, 
                NewFolderIcon, 
                NewFolderPinned,
                CreateNewFolder); // createSubfolder parameter
            
            CustomFolders.Add(folder);
            
            IsAddingFolder = false;
            ResetNewFolderFields();
            
            _notificationService.ShowSuccess("Folder Created", $"Folder '{folder.Name}' created at {folder.Path}");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Error", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Opens folder browser for new custom folder path.
    /// </summary>
    [RelayCommand]
    private void BrowseNewFolderPath()
    {
        var path = ShowFolderBrowserDialog();
        if (!string.IsNullOrEmpty(path))
        {
            NewFolderPath = path;
            if (string.IsNullOrWhiteSpace(NewFolderName))
            {
                NewFolderName = Path.GetFileName(path);
            }
        }
    }

    /// <summary>
    /// Toggles pinned state for a custom folder.
    /// </summary>
    [RelayCommand]
    private async Task ToggleFolderPinnedAsync(CustomFolder? folder)
    {
        if (folder == null) return;

        try
        {
            folder.IsPinned = !folder.IsPinned;
            await _settingsService.UpdateCustomFolderAsync(folder);
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Error", ex.Message);
        }
    }

    /// <summary>
    /// Edits a custom folder.
    /// </summary>
    [RelayCommand]
    private async Task EditFolderPathAsync(CustomFolder? folder)
    {
        if (folder == null) return;

        var newPath = ShowFolderBrowserDialog(folder.Path);
        if (string.IsNullOrEmpty(newPath) || newPath == folder.Path)
            return;

        IsBusy = true;
        try
        {
            folder.Path = newPath;
            await _settingsService.UpdateCustomFolderAsync(folder);
            _notificationService.ShowSuccess("Folder Updated", $"Path for '{folder.Name}' updated.");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Error", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Deletes a custom folder.
    /// </summary>
    [RelayCommand]
    private async Task DeleteFolderAsync(CustomFolder? folder)
    {
        if (folder == null) return;

        IsBusy = true;
        try
        {
            var removed = await _settingsService.RemoveCustomFolderAsync(folder.Id);
            if (removed)
            {
                CustomFolders.Remove(folder);
                SelectedFolder = null;
                _notificationService.ShowSuccess("Folder Deleted", $"Custom folder '{folder.Name}' deleted.");
            }
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Error", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ResetNewFolderFields()
    {
        NewFolderName = string.Empty;
        NewFolderPath = string.Empty;
        NewFolderIcon = "📁";
        NewFolderPinned = false;
        CreateNewFolder = true;
    }

    #endregion

    #region Import/Export

    /// <summary>
    /// Exports rules to a JSON file.
    /// </summary>
    [RelayCommand]
    private async Task ExportRulesAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Rules",
            Filter = "JSON Files (*.json)|*.json",
            FileName = $"autodrop-rules-{DateTime.Now:yyyyMMdd}.json"
        };

        if (dialog.ShowDialog() != true)
            return;

        IsBusy = true;
        try
        {
            var rules = await _ruleService.GetAllRulesAsync();
            var json = System.Text.Json.JsonSerializer.Serialize(
                new { rules, exportedAt = DateTime.UtcNow },
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            
            await File.WriteAllTextAsync(dialog.FileName, json);
            _notificationService.ShowSuccess("Export Complete", $"Exported {rules.Count} rules.");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Export Failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Imports rules from a JSON file.
    /// </summary>
    [RelayCommand]
    private async Task ImportRulesAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import Rules",
            Filter = "JSON Files (*.json)|*.json"
        };

        if (dialog.ShowDialog() != true)
            return;

        IsBusy = true;
        try
        {
            var json = await File.ReadAllTextAsync(dialog.FileName);
            var importData = System.Text.Json.JsonSerializer.Deserialize<ImportData>(json);
            
            if (importData?.Rules == null || importData.Rules.Count == 0)
            {
                _notificationService.ShowError("Import Failed", "No rules found in the file.");
                return;
            }

            var importedCount = 0;
            foreach (var rule in importData.Rules)
            {
                await _ruleService.SaveRuleAsync(rule.Extension, rule.Destination, rule.AutoMove);
                importedCount++;
            }

            await LoadRulesAsync();
            _notificationService.ShowSuccess("Import Complete", $"Imported {importedCount} rules.");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("Import Failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private sealed class ImportData
    {
        public List<FileRule>? Rules { get; set; }
    }

    #endregion

    #region Helpers

    private static string? ShowFolderBrowserDialog(string? initialDirectory = null)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Folder"
        };

        if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    #endregion
}
