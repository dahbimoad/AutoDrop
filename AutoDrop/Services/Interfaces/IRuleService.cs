using AutoDrop.Models;

namespace AutoDrop.Services.Interfaces;

/// <summary>
/// Service responsible for managing file rules and persistence.
/// </summary>
public interface IRuleService
{
    /// <summary>
    /// Gets all stored rules.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of file rules.</returns>
    Task<IReadOnlyList<FileRule>> GetAllRulesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a rule for a specific file extension.
    /// </summary>
    /// <param name="extension">File extension (e.g., ".jpg").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rule if found, null otherwise.</returns>
    Task<FileRule?> GetRuleForExtensionAsync(string extension, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or updates a rule for a file extension.
    /// </summary>
    /// <param name="extension">File extension.</param>
    /// <param name="destination">Destination folder path.</param>
    /// <param name="autoMove">Whether to auto-move without popup.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created or updated rule.</returns>
    Task<FileRule> SaveRuleAsync(string extension, string destination, bool autoMove = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing rule.
    /// </summary>
    /// <param name="rule">The rule to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateRuleAsync(FileRule rule, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a rule for a specific extension.
    /// </summary>
    /// <param name="extension">File extension.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if rule was removed.</returns>
    Task<bool> RemoveRuleAsync(string extension, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the usage statistics for a rule.
    /// </summary>
    /// <param name="extension">File extension.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateRuleUsageAsync(string extension, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles the auto-move setting for a rule.
    /// </summary>
    /// <param name="extension">File extension.</param>
    /// <param name="autoMove">Whether to enable auto-move.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetAutoMoveAsync(string extension, bool autoMove, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles the enabled state for a rule.
    /// </summary>
    /// <param name="extension">File extension.</param>
    /// <param name="isEnabled">Whether the rule is enabled.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetRuleEnabledAsync(string extension, bool isEnabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates all rules that have a specific destination path to a new path.
    /// Used when a custom folder's path changes.
    /// </summary>
    /// <param name="oldPath">The old destination path.</param>
    /// <param name="newPath">The new destination path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of rules updated.</returns>
    Task<int> UpdateRulesDestinationAsync(string oldPath, string newPath, CancellationToken cancellationToken = default);
}
