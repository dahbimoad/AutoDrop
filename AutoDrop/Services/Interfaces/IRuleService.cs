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
    /// <returns>Collection of file rules.</returns>
    Task<IReadOnlyList<FileRule>> GetAllRulesAsync();

    /// <summary>
    /// Gets a rule for a specific file extension.
    /// </summary>
    /// <param name="extension">File extension (e.g., ".jpg").</param>
    /// <returns>The rule if found, null otherwise.</returns>
    Task<FileRule?> GetRuleForExtensionAsync(string extension);

    /// <summary>
    /// Adds or updates a rule for a file extension.
    /// </summary>
    /// <param name="extension">File extension.</param>
    /// <param name="destination">Destination folder path.</param>
    /// <param name="autoMove">Whether to auto-move without popup.</param>
    /// <returns>The created or updated rule.</returns>
    Task<FileRule> SaveRuleAsync(string extension, string destination, bool autoMove = false);

    /// <summary>
    /// Updates an existing rule.
    /// </summary>
    /// <param name="rule">The rule to update.</param>
    Task UpdateRuleAsync(FileRule rule);

    /// <summary>
    /// Removes a rule for a specific extension.
    /// </summary>
    /// <param name="extension">File extension.</param>
    /// <returns>True if rule was removed.</returns>
    Task<bool> RemoveRuleAsync(string extension);

    /// <summary>
    /// Updates the usage statistics for a rule.
    /// </summary>
    /// <param name="extension">File extension.</param>
    Task UpdateRuleUsageAsync(string extension);

    /// <summary>
    /// Toggles the auto-move setting for a rule.
    /// </summary>
    /// <param name="extension">File extension.</param>
    /// <param name="autoMove">Whether to enable auto-move.</param>
    Task SetAutoMoveAsync(string extension, bool autoMove);

    /// <summary>
    /// Toggles the enabled state for a rule.
    /// </summary>
    /// <param name="extension">File extension.</param>
    /// <param name="isEnabled">Whether the rule is enabled.</param>
    Task SetRuleEnabledAsync(string extension, bool isEnabled);
}
