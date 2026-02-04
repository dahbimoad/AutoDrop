using AutoDrop.Models;

namespace AutoDrop.Services.Interfaces;

/// <summary>
/// Service for organizing files within a dropped folder based on various criteria.
/// Supports organization by extension, category, size, date, and AI content analysis.
/// </summary>
public interface IFolderOrganizationService
{
    /// <summary>
    /// Scans a folder and generates a preview of how files would be organized.
    /// Does not perform any file operations.
    /// </summary>
    /// <param name="folderPath">The folder to scan.</param>
    /// <param name="settings">Organization settings.</param>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of folder groups showing planned organization.</returns>
    Task<IReadOnlyList<PlannedFolderGroup>> PreviewOrganizationAsync(
        string folderPath,
        FolderOrganizationSettings settings,
        IProgress<FolderOrganizationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the folder organization, moving files to their destination folders.
    /// </summary>
    /// <param name="folderPath">The folder being organized.</param>
    /// <param name="groups">The planned folder groups to execute.</param>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the organization operation.</returns>
    Task<FolderOrganizationResult> ExecuteOrganizationAsync(
        string folderPath,
        IEnumerable<PlannedFolderGroup> groups,
        IProgress<FolderOrganizationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a folder can be organized (permissions, not empty, etc.).
    /// </summary>
    /// <param name="folderPath">The folder to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with any error message.</returns>
    Task<(bool IsValid, string? Error)> ValidateFolderAsync(
        string folderPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of files in a folder that would be processed.
    /// </summary>
    /// <param name="folderPath">The folder to count.</param>
    /// <param name="includeSubdirectories">Whether to include subdirectories.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of files that would be processed.</returns>
    Task<int> GetFileCountAsync(
        string folderPath,
        bool includeSubdirectories,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the size range category for a file size.
    /// </summary>
    /// <param name="sizeInBytes">File size in bytes.</param>
    /// <returns>Size range category.</returns>
    FileSizeRange GetSizeRange(long sizeInBytes);

    /// <summary>
    /// Gets a display name for a size range.
    /// </summary>
    /// <param name="range">The size range.</param>
    /// <returns>Display name for the range.</returns>
    string GetSizeRangeDisplayName(FileSizeRange range);
}
