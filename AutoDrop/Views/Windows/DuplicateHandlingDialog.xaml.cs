using System.Windows;
using AutoDrop.Converters;
using AutoDrop.Models;
using AutoDrop.Services.Interfaces;

namespace AutoDrop.Views.Windows;

/// <summary>
/// Dialog for handling duplicate files during move operations.
/// </summary>
public partial class DuplicateHandlingDialog : Wpf.Ui.Controls.FluentWindow
{
    private readonly DuplicateCheckResult _duplicateResult;
    private readonly bool _hasMoreDuplicates;

    /// <summary>
    /// Gets the user's selected handling choice.
    /// </summary>
    public DuplicateHandling SelectedHandling { get; private set; } = DuplicateHandling.KeepBothAll;

    /// <summary>
    /// Gets whether the user wants to apply this choice to all remaining duplicates.
    /// </summary>
    public bool ApplyToAll => ApplyToAllCheckBox.IsChecked == true;

    /// <summary>
    /// Gets whether the dialog was cancelled.
    /// </summary>
    public bool WasCancelled { get; private set; } = true;

    public DuplicateHandlingDialog(
        DuplicateCheckResult duplicateResult,
        bool hasMoreDuplicates = false)
    {
        _duplicateResult = duplicateResult ?? throw new ArgumentNullException(nameof(duplicateResult));
        _hasMoreDuplicates = hasMoreDuplicates;

        InitializeComponent();
        PopulateUI();
    }

    private void PopulateUI()
    {
        // File name
        FileNameText.Text = Path.GetFileName(_duplicateResult.Source?.FilePath ?? "Unknown");

        // Source file info
        if (_duplicateResult.Source != null)
        {
            SourceSizeText.Text = $"Size: {FormatFileSize(_duplicateResult.Source.Size)}";
            SourceDateText.Text = $"Modified: {_duplicateResult.Source.LastModified:g}";
        }

        // Destination file info
        if (_duplicateResult.Destination != null)
        {
            DestSizeText.Text = $"Size: {FormatFileSize(_duplicateResult.Destination.Size)}";
            DestDateText.Text = $"Modified: {_duplicateResult.Destination.LastModified:g}";
        }

        // Show match status for exact duplicates
        if (_duplicateResult.IsExactMatch)
        {
            MatchStatusBorder.Visibility = Visibility.Visible;
            MatchStatusText.Text = _duplicateResult.ComparisonMethod == DuplicateComparisonMethod.Hash
                ? "Files are identical (verified by content hash)"
                : "Files appear identical (same size and date)";
            
            // Show delete source option for exact matches
            DeleteSourceButton.Visibility = Visibility.Visible;
        }

        // Show "Apply to all" checkbox if there are more duplicates
        if (_hasMoreDuplicates)
        {
            ApplyToAllCheckBox.Visibility = Visibility.Visible;
        }
    }

    private static string FormatFileSize(long bytes)
    {
        return FileSizeConverter.Convert(bytes);
    }

    private void OnKeepBothClick(object sender, RoutedEventArgs e)
    {
        SelectedHandling = ApplyToAll ? DuplicateHandling.KeepBothAll : DuplicateHandling.Ask;
        WasCancelled = false;
        DialogResult = true;
        Close();
    }

    private void OnReplaceClick(object sender, RoutedEventArgs e)
    {
        SelectedHandling = ApplyToAll ? DuplicateHandling.ReplaceAll : DuplicateHandling.Ask;
        WasCancelled = false;
        DialogResult = true;
        Close();
    }

    private void OnSkipClick(object sender, RoutedEventArgs e)
    {
        SelectedHandling = ApplyToAll ? DuplicateHandling.SkipAll : DuplicateHandling.Ask;
        WasCancelled = false;
        DialogResult = true;
        Close();
    }

    private void OnDeleteSourceClick(object sender, RoutedEventArgs e)
    {
        SelectedHandling = ApplyToAll ? DuplicateHandling.DeleteSourceAll : DuplicateHandling.Ask;
        WasCancelled = false;
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        WasCancelled = true;
        DialogResult = false;
        Close();
    }
}
