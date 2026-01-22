namespace AutoDrop.Services.Interfaces;

/// <summary>
/// Service responsible for managing application windows.
/// Provides abstraction between ViewModels and Views for proper MVVM decoupling.
/// </summary>
public interface IWindowService
{
    /// <summary>
    /// Opens the Rules Manager window as a dialog.
    /// </summary>
    void ShowRulesManager();

    /// <summary>
    /// Shows the main drop zone window.
    /// </summary>
    void ShowDropZone();

    /// <summary>
    /// Hides the main drop zone window.
    /// </summary>
    void HideDropZone();

    /// <summary>
    /// Shows a folder browser dialog.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="initialDirectory">Initial directory to show.</param>
    /// <returns>Selected folder path, or null if cancelled.</returns>
    string? ShowFolderBrowserDialog(string? title = null, string? initialDirectory = null);

    /// <summary>
    /// Shows a save file dialog.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="filter">File filter (e.g., "JSON Files (*.json)|*.json").</param>
    /// <param name="defaultFileName">Default file name.</param>
    /// <returns>Selected file path, or null if cancelled.</returns>
    string? ShowSaveFileDialog(string? title = null, string? filter = null, string? defaultFileName = null);

    /// <summary>
    /// Shows an open file dialog.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="filter">File filter.</param>
    /// <returns>Selected file path, or null if cancelled.</returns>
    string? ShowOpenFileDialog(string? title = null, string? filter = null);

    /// <summary>
    /// Opens the AI Settings window as a dialog.
    /// </summary>
    void ShowAiSettings();
}
