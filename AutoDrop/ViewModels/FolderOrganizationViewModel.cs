using System.Collections.ObjectModel;
using AutoDrop.Models;
using AutoDrop.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AutoDrop.ViewModels;

/// <summary>
/// ViewModel for the folder organization window.
/// Allows users to organize files within a dropped folder by various criteria.
/// </summary>
public partial class FolderOrganizationViewModel : Base.ViewModelBase, IDisposable
{
    private readonly IFolderOrganizationService _organizationService;
    private readonly IFileOperationService _fileOperationService;
    private readonly INotificationService _notificationService;
    private readonly IUndoService _undoService;
    private readonly IAiService _aiService;
    private readonly ILogger<FolderOrganizationViewModel> _logger;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    #region Observable Properties

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviewCommand))]
    [NotifyCanExecuteChangedFor(nameof(OrganizeCommand))]
    private string _folderPath = string.Empty;

    [ObservableProperty]
    private string _folderName = string.Empty;

    [ObservableProperty]
    private int _totalFileCount;

    [ObservableProperty]
    private int _selectedFileCount;

    [ObservableProperty]
    private int _groupCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDateOptionsVisible))]
    [NotifyPropertyChangedFor(nameof(IsAiOptionsVisible))]
    [NotifyCanExecuteChangedFor(nameof(PreviewCommand))]
    private OrganizationCriteria _selectedCriteria = OrganizationCriteria.ByExtension;

    [ObservableProperty]
    private DateOrganizationFormat _selectedDateFormat = DateOrganizationFormat.YearMonth;

    [ObservableProperty]
    private bool _useCreationDate;

    [ObservableProperty]
    private bool _includeSubdirectories;

    [ObservableProperty]
    private bool _skipHiddenFiles = true;

    [ObservableProperty]
    private int _maxAiFiles = 20;

    [ObservableProperty]
    private int _maxFileSizeMb = 10;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviewCommand))]
    [NotifyCanExecuteChangedFor(nameof(OrganizeCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _isPreviewed;

    [ObservableProperty]
    private string _progressStatus = string.Empty;

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private string _currentFile = string.Empty;

    [ObservableProperty]
    private bool _isAiAvailable;

    [ObservableProperty]
    private string _aiStatusMessage = string.Empty;

    #endregion

    #region Collections

    /// <summary>
    /// Available organization criteria options.
    /// </summary>
    public IReadOnlyList<OrganizationCriteriaOption> CriteriaOptions { get; } =
    [
        new("By Extension", OrganizationCriteria.ByExtension, "Group files by their file extension (.pdf, .jpg, etc.)"),
        new("By Category", OrganizationCriteria.ByCategory, "Group files by type (Documents, Images, Videos, etc.)"),
        new("By Size", OrganizationCriteria.BySize, "Group files by size (Tiny, Small, Medium, Large, Huge)"),
        new("By Date", OrganizationCriteria.ByDate, "Group files by creation or modification date"),
        new("By Name", OrganizationCriteria.ByName, "Group files by filename patterns (IMG_, Screenshot_, Invoice_)"),
        new("By Content", OrganizationCriteria.ByContent, "Use AI to analyze and categorize files by content")
    ];

    /// <summary>
    /// Available date format options.
    /// </summary>
    public IReadOnlyList<DateFormatOption> DateFormatOptions { get; } =
    [
        new("Year (2026)", DateOrganizationFormat.Year),
        new("Year-Month (2026-01)", DateOrganizationFormat.YearMonth),
        new("Year-Month-Day (2026-01-15)", DateOrganizationFormat.YearMonthDay)
    ];

    /// <summary>
    /// Planned folder groups after preview.
    /// </summary>
    public ObservableCollection<PlannedFolderGroupViewModel> Groups { get; } = [];

    #endregion

    #region Computed Properties

    public bool IsDateOptionsVisible => SelectedCriteria == OrganizationCriteria.ByDate;
    public bool IsAiOptionsVisible => SelectedCriteria is OrganizationCriteria.ByContent or OrganizationCriteria.ByName;

    #endregion

    /// <summary>
    /// Event raised to request closing the dialog.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Event raised when organization is complete.
    /// </summary>
    public event EventHandler<FolderOrganizationResult>? OrganizationCompleted;

    public FolderOrganizationViewModel(
        IFolderOrganizationService organizationService,
        IFileOperationService fileOperationService,
        INotificationService notificationService,
        IUndoService undoService,
        IAiService aiService,
        ILogger<FolderOrganizationViewModel> logger)
    {
        _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
        _fileOperationService = fileOperationService ?? throw new ArgumentNullException(nameof(fileOperationService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _undoService = undoService ?? throw new ArgumentNullException(nameof(undoService));
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("FolderOrganizationViewModel initialized");
    }

    /// <summary>
    /// Initializes the view model with a folder path.
    /// </summary>
    public async Task InitializeAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        FolderPath = folderPath;
        FolderName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar));

        _logger.LogInformation("Initializing folder organization for: {Folder}", FolderName);

        // Validate the folder
        var (isValid, error) = await _organizationService
            .ValidateFolderAsync(folderPath, cancellationToken)
            .ConfigureAwait(false);

        if (!isValid)
        {
            _logger.LogWarning("Folder validation failed: {Error}", error);
            _notificationService.ShowError("Invalid Folder", error ?? "Cannot organize this folder.");
            CloseRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Get file count
        TotalFileCount = await _organizationService
            .GetFileCountAsync(folderPath, false, cancellationToken)
            .ConfigureAwait(false);

        // Check AI availability
        await CheckAiAvailabilityAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Folder has {Count} files", TotalFileCount);
    }

    private async Task CheckAiAvailabilityAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _aiService.RefreshAvailabilityAsync(cancellationToken).ConfigureAwait(false);
            IsAiAvailable = _aiService.IsAvailable;
            
            if (IsAiAvailable)
            {
                AiStatusMessage = $"AI ready ({_aiService.ActiveProvider})";
            }
            else
            {
                AiStatusMessage = "AI not configured - configure in Settings";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check AI availability");
            IsAiAvailable = false;
            AiStatusMessage = "AI unavailable";
        }
    }

    [RelayCommand(CanExecute = nameof(CanPreview))]
    private async Task PreviewAsync()
    {
        _logger.LogInformation("Generating preview for {Folder} with criteria {Criteria}",
            FolderName, SelectedCriteria);

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        IsProcessing = true;
        IsPreviewed = false;
        Groups.Clear();

        var progress = new Progress<FolderOrganizationProgress>(OnProgressUpdate);

        try
        {
            var settings = CreateSettings();

            var groups = await _organizationService
                .PreviewOrganizationAsync(FolderPath, settings, progress, _cts.Token)
                .ConfigureAwait(false);

            // Update UI on main thread
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var group in groups)
                {
                    var groupVm = new PlannedFolderGroupViewModel(group);
                    groupVm.PropertyChanged += GroupVm_PropertyChanged;
                    Groups.Add(groupVm);
                }

                GroupCount = Groups.Count;
                SelectedFileCount = Groups.Sum(g => g.SelectedCount);
                IsPreviewed = true;

                _logger.LogInformation("Preview complete: {Groups} groups, {Files} files",
                    GroupCount, SelectedFileCount);
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Preview cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Preview failed");
            _notificationService.ShowError("Preview Failed", ex.Message);
        }
        finally
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                IsProcessing = false;
                ProgressStatus = string.Empty;
                ProgressPercent = 0;
                CurrentFile = string.Empty;
            });
        }
    }

    private bool CanPreview() => !IsProcessing && !string.IsNullOrEmpty(FolderPath) &&
                                  (SelectedCriteria != OrganizationCriteria.ByContent || IsAiAvailable);

    [RelayCommand(CanExecute = nameof(CanOrganize))]
    private async Task OrganizeAsync()
    {
        if (!IsPreviewed || Groups.Count == 0)
        {
            _notificationService.ShowError("Preview Required", "Please preview the organization first.");
            return;
        }

        _logger.LogInformation("Executing organization for {Folder}", FolderName);

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        IsProcessing = true;
        var progress = new Progress<FolderOrganizationProgress>(OnProgressUpdate);

        try
        {
            var groupModels = Groups.Select(g => g.ToModel()).ToList();

            var result = await _organizationService
                .ExecuteOrganizationAsync(FolderPath, groupModels, progress, _cts.Token)
                .ConfigureAwait(false);

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (result.Success)
                {
                    // Register for undo - each operation gets its own undo action
                    foreach (var operation in result.Operations)
                    {
                        _undoService.RegisterOperation(
                            operation.ItemName,
                            async () => await _fileOperationService.UndoMoveAsync(operation),
                            expirationSeconds: 15);
                    }

                    _notificationService.ShowSuccess(
                        "Organization Complete",
                        $"Organized {result.MovedCount} files into {result.FoldersCreated} folders");

                    _logger.LogInformation("Organization complete: {Moved} moved, {Created} folders",
                        result.MovedCount, result.FoldersCreated);
                }
                else
                {
                    _notificationService.ShowError(
                        "Organization Failed",
                        result.ErrorMessage ?? $"Failed to move {result.FailedCount} files");
                }

                OrganizationCompleted?.Invoke(this, result);
                CloseRequested?.Invoke(this, EventArgs.Empty);
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Organization cancelled");
            _notificationService.ShowInfo("Cancelled", "Organization was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Organization failed");
            _notificationService.ShowError("Organization Failed", ex.Message);
        }
        finally
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                IsProcessing = false;
                ProgressStatus = string.Empty;
                ProgressPercent = 0;
                CurrentFile = string.Empty;
            });
        }
    }

    private bool CanOrganize() => !IsProcessing && IsPreviewed && SelectedFileCount > 0;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cts?.Cancel();
        _logger.LogDebug("Cancellation requested");
    }

    private bool CanCancel() => IsProcessing;

    [RelayCommand]
    private void Close()
    {
        _cts?.Cancel();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var group in Groups)
        {
            group.IsAllSelected = true;
        }
        UpdateSelectedCount();
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var group in Groups)
        {
            group.IsAllSelected = false;
        }
        UpdateSelectedCount();
    }

    [RelayCommand]
    private void SelectCriteria(string criteria)
    {
        if (Enum.TryParse<OrganizationCriteria>(criteria, out var parsed))
        {
            SelectedCriteria = parsed;
            IsPreviewed = false;
            Groups.Clear();
            _logger.LogDebug("Criteria changed to {Criteria}", parsed);
        }
    }

    private void GroupVm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlannedFolderGroupViewModel.SelectedCount))
        {
            UpdateSelectedCount();
        }
    }

    private void UpdateSelectedCount()
    {
        SelectedFileCount = Groups.Sum(g => g.SelectedCount);
        OrganizeCommand.NotifyCanExecuteChanged();
    }

    private FolderOrganizationSettings CreateSettings()
    {
        return new FolderOrganizationSettings
        {
            Criteria = SelectedCriteria,
            IncludeSubdirectories = IncludeSubdirectories,
            SkipHiddenFiles = SkipHiddenFiles,
            DateFormat = SelectedDateFormat,
            UseCreationDate = UseCreationDate,
            MaxAiFilesPerOperation = MaxAiFiles,
            MaxAiFileSizeMb = MaxFileSizeMb,
            AiDelayMs = 500 // Fixed delay for rate limiting
        };
    }

    private void OnProgressUpdate(FolderOrganizationProgress progress)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            ProgressStatus = progress.Status;
            ProgressPercent = progress.ProgressPercent;
            CurrentFile = progress.CurrentFile;
        });
    }

    public void Dispose()
    {
        if (_disposed) return;

        _cts?.Cancel();
        _cts?.Dispose();

        foreach (var group in Groups)
        {
            group.PropertyChanged -= GroupVm_PropertyChanged;
        }

        _disposed = true;
        _logger.LogDebug("FolderOrganizationViewModel disposed");
    }
}

#region Helper Classes

/// <summary>
/// Option for organization criteria selection.
/// </summary>
public sealed record OrganizationCriteriaOption(
    string DisplayName,
    OrganizationCriteria Value,
    string Description);

/// <summary>
/// Option for date format selection.
/// </summary>
public sealed record DateFormatOption(
    string DisplayName,
    DateOrganizationFormat Value);

/// <summary>
/// ViewModel wrapper for PlannedFolderGroup with observable properties.
/// </summary>
public partial class PlannedFolderGroupViewModel : ObservableObject
{
    private readonly PlannedFolderGroup _model;

    public PlannedFolderGroupViewModel(PlannedFolderGroup model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _files = new ObservableCollection<PlannedFileMoveViewModel>(
            model.Files.Select(f => new PlannedFileMoveViewModel(f)));

        foreach (var file in _files)
        {
            file.PropertyChanged += File_PropertyChanged;
        }
    }

    public string DestinationFolder => _model.DestinationFolder;
    public string FolderName => _model.FolderName;
    public string GroupKey => _model.GroupKey;
    public int FileCount => _model.FileCount;
    public long TotalSize => _model.TotalSize;
    public bool FolderExists => _model.FolderExists;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCount))]
    private ObservableCollection<PlannedFileMoveViewModel> _files;

    public int SelectedCount => Files.Count(f => f.IsSelected);

    public bool IsAllSelected
    {
        get => Files.All(f => f.IsSelected);
        set
        {
            foreach (var file in Files)
            {
                file.IsSelected = value;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedCount));
        }
    }

    private void File_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlannedFileMoveViewModel.IsSelected))
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(IsAllSelected));
        }
    }

    public PlannedFolderGroup ToModel()
    {
        return new PlannedFolderGroup
        {
            DestinationFolder = _model.DestinationFolder,
            GroupKey = _model.GroupKey,
            Files = Files.Select(f => f.ToModel()).ToList()
        };
    }
}

/// <summary>
/// ViewModel wrapper for PlannedFileMove with observable properties.
/// </summary>
public partial class PlannedFileMoveViewModel : ObservableObject
{
    private readonly PlannedFileMove _model;

    public PlannedFileMoveViewModel(PlannedFileMove model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _isSelected = model.IsSelected;
    }

    public string SourcePath => _model.SourcePath;
    public string FileName => _model.FileName;
    public string DestinationFolder => _model.DestinationFolder;
    public long Size => _model.Size;
    public string Extension => _model.Extension;
    public string Category => _model.Category;
    public double? AiConfidence => _model.AiConfidence;
    public string? AiCategory => _model.AiCategory;
    public string? SkipReason => _model.SkipReason;
    public bool IsSkipped => _model.IsSkipped;

    [ObservableProperty]
    private bool _isSelected;

    public PlannedFileMove ToModel()
    {
        return new PlannedFileMove
        {
            SourcePath = _model.SourcePath,
            DestinationFolder = _model.DestinationFolder,
            Size = _model.Size,
            Category = _model.Category,
            AiConfidence = _model.AiConfidence,
            AiCategory = _model.AiCategory,
            SkipReason = _model.SkipReason,
            IsSelected = IsSelected
        };
    }
}

#endregion
