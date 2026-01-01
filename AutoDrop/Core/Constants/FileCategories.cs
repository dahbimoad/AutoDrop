namespace AutoDrop.Core.Constants;

/// <summary>
/// File category constants and extension mappings.
/// </summary>
public static class FileCategories
{
    public const string Image = "Image";
    public const string Document = "Document";
    public const string Video = "Video";
    public const string Audio = "Audio";
    public const string Archive = "Archive";
    public const string Installer = "Installer";
    public const string Unknown = "Unknown";

    /// <summary>
    /// Extension to category mappings.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ExtensionMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Images
        { ".jpg", Image },
        { ".jpeg", Image },
        { ".png", Image },
        { ".gif", Image },
        { ".bmp", Image },
        { ".webp", Image },
        { ".ico", Image },
        { ".svg", Image },
        { ".tiff", Image },
        { ".tif", Image },

        // Documents
        { ".pdf", Document },
        { ".doc", Document },
        { ".docx", Document },
        { ".xls", Document },
        { ".xlsx", Document },
        { ".ppt", Document },
        { ".pptx", Document },
        { ".txt", Document },
        { ".rtf", Document },
        { ".odt", Document },
        { ".ods", Document },
        { ".odp", Document },
        { ".csv", Document },
        { ".md", Document },

        // Videos
        { ".mp4", Video },
        { ".avi", Video },
        { ".mkv", Video },
        { ".mov", Video },
        { ".wmv", Video },
        { ".flv", Video },
        { ".webm", Video },
        { ".m4v", Video },

        // Audio
        { ".mp3", Audio },
        { ".wav", Audio },
        { ".flac", Audio },
        { ".aac", Audio },
        { ".ogg", Audio },
        { ".wma", Audio },
        { ".m4a", Audio },

        // Archives
        { ".zip", Archive },
        { ".rar", Archive },
        { ".7z", Archive },
        { ".tar", Archive },
        { ".gz", Archive },
        { ".bz2", Archive },

        // Installers
        { ".exe", Installer },
        { ".msi", Installer },
        { ".msix", Installer }
    };

    /// <summary>
    /// Gets the category for a given file extension.
    /// </summary>
    /// <param name="extension">File extension (with or without leading dot).</param>
    /// <returns>The file category.</returns>
    public static string GetCategory(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return Unknown;

        var normalizedExtension = extension.StartsWith('.')
            ? extension
            : $".{extension}";

        return ExtensionMappings.TryGetValue(normalizedExtension, out var category)
            ? category
            : Unknown;
    }
}
