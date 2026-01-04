using AutoDrop.Models;

namespace AutoDrop.Services.Interfaces;

/// <summary>
/// Service responsible for managing application settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the current application settings.
    /// </summary>
    /// <returns>The application settings.</returns>
    Task<AppSettings> GetSettingsAsync();

    /// <summary>
    /// Saves the application settings.
    /// </summary>
    /// <param name="settings">The settings to save.</param>
    Task SaveSettingsAsync(AppSettings settings);

    /// <summary>
    /// Gets all custom folders.
    /// </summary>
    /// <returns>Collection of custom folders.</returns>
    Task<IReadOnlyList<CustomFolder>> GetCustomFoldersAsync();

    /// <summary>
    /// Adds a new custom folder.
    /// </summary>
    /// <param name="name">Display name for the folder.</param>
    /// <param name="path">Parent path where folder will be created (or full path if createSubfolder is false).</param>
    /// <param name="icon">Icon identifier.</param>
    /// <param name="isPinned">Whether the folder is pinned.</param>
    /// <param name="createSubfolder">If true, creates a subfolder with the name inside path. If false, uses path directly.</param>
    /// <returns>The created custom folder.</returns>
    Task<CustomFolder> AddCustomFolderAsync(string name, string path, string icon = "📁", bool isPinned = false, bool createSubfolder = true);

    /// <summary>
    /// Updates an existing custom folder.
    /// </summary>
    /// <param name="folder">The folder to update.</param>
    Task UpdateCustomFolderAsync(CustomFolder folder);

    /// <summary>
    /// Removes a custom folder.
    /// </summary>
    /// <param name="folderId">The folder ID to remove.</param>
    /// <returns>True if folder was removed.</returns>
    Task<bool> RemoveCustomFolderAsync(Guid folderId);

    /// <summary>
    /// Updates the usage count for a custom folder.
    /// </summary>
    /// <param name="folderId">The folder ID.</param>
    Task IncrementFolderUsageAsync(Guid folderId);
}
