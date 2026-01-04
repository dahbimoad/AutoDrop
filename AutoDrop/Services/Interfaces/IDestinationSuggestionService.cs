using AutoDrop.Models;

namespace AutoDrop.Services.Interfaces;

/// <summary>
/// Service responsible for suggesting destination folders based on file type and user rules.
/// </summary>
public interface IDestinationSuggestionService
{
    /// <summary>
    /// Gets destination suggestions for a dropped item.
    /// </summary>
    /// <param name="item">The dropped file or folder.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of suggested destinations.</returns>
    Task<IReadOnlyList<DestinationSuggestion>> GetSuggestionsAsync(DroppedItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the default destination folder for a file category.
    /// </summary>
    /// <param name="category">File category.</param>
    /// <returns>Default folder path.</returns>
    string GetDefaultDestination(string category);
}
