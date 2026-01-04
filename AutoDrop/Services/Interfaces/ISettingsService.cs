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
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The application settings.</returns>
    Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the application settings.
    /// </summary>
    /// <param name="settings">The settings to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all custom folders.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of custom folders.</returns>
    Task<IReadOnlyList<CustomFolder>> GetCustomFoldersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new custom folder.
    /// </summary>
    /// <param name="name">Display name for the folder.</param>
    /// <param name="path">Parent path where folder will be created (or full path if createSubfolder is false).</param>
    /// <param name="icon">Icon identifier.</param>
    /// <param name="isPinned">Whether the folder is pinned.</param>
    /// <param name="createSubfolder">If true, creates a subfolder with the name inside path. If false, uses path directly.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created custom folder.</returns>
    Task<CustomFolder> AddCustomFolderAsync(string name, string path, string icon = "📁", bool isPinned = false, bool createSubfolder = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing custom folder.
    /// </summary>
    /// <param name="folder">The folder to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateCustomFolderAsync(CustomFolder folder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a custom folder.
    /// </summary>
    /// <param name="folderId">The folder ID to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if folder was removed.</returns>
    Task<bool> RemoveCustomFolderAsync(Guid folderId, CancellationToken cancellationToken = default);
}
