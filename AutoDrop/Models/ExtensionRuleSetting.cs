using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoDrop.Models;

/// <summary>
/// Represents the user's choice to remember/auto-move files of a specific extension.
/// Used in the suggestion popup when multiple files with different extensions are dropped.
/// </summary>
public partial class ExtensionRuleSetting : ObservableObject
{
    /// <summary>
    /// The file extension (e.g., ".png", ".jpg").
    /// </summary>
    public required string Extension { get; init; }

    /// <summary>
    /// Number of files with this extension in the current drop.
    /// </summary>
    public int FileCount { get; init; }

    /// <summary>
    /// Whether to remember this destination for files with this extension.
    /// </summary>
    [ObservableProperty]
    private bool _rememberChoice;

    /// <summary>
    /// Whether to enable auto-move (skip popup) for this extension.
    /// Only relevant when RememberChoice is true.
    /// </summary>
    [ObservableProperty]
    private bool _enableAutoMove;

    /// <summary>
    /// Display text for the checkbox (e.g., "Always move .png files here (2 files)").
    /// </summary>
    public string DisplayText => FileCount > 1 
        ? $"Always move {Extension} files here ({FileCount} files)"
        : $"Always move {Extension} files here";
}
