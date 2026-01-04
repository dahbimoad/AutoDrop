namespace AutoDrop.Models;

/// <summary>
/// Represents a file or folder that has been dropped onto the drop zone.
/// </summary>
public sealed class DroppedItem
{
    /// <summary>
    /// Full path to the dropped file or folder.
    /// </summary>
    public required string FullPath { get; init; }

    /// <summary>
    /// File or folder name (without path).
    /// </summary>
    public string Name => Path.GetFileName(FullPath);

    /// <summary>
    /// File extension (empty for folders).
    /// </summary>
    public string Extension => IsDirectory ? string.Empty : Path.GetExtension(FullPath);

    /// <summary>
    /// Whether this item is a directory.
    /// </summary>
    public bool IsDirectory { get; init; }

    /// <summary>
    /// File size in bytes (0 for directories).
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// Detected category based on file extension.
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Creates a DroppedItem from a file path.
    /// </summary>
    /// <param name="path">The full path to the file or folder.</param>
    /// <returns>A new DroppedItem instance.</returns>
    /// <exception cref="ArgumentException">Thrown when path is null or whitespace.</exception>
    public static DroppedItem FromPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        
        bool isDirectory;
        try
        {
            isDirectory = Directory.Exists(path);
        }
        catch (Exception)
        {
            // If we can't determine, assume it's a file
            isDirectory = false;
        }
        
        var category = isDirectory 
            ? "Folder" 
            : Core.Constants.FileCategories.GetCategory(Path.GetExtension(path));

        long size = 0;
        try
        {
            if (!isDirectory && File.Exists(path))
            {
                var fileInfo = new FileInfo(path);
                size = fileInfo.Length;
            }
        }
        catch (IOException)
        {
            // File may be locked or inaccessible, continue with size = 0
        }
        catch (UnauthorizedAccessException)
        {
            // No permission to access file, continue with size = 0
        }
        catch (System.Security.SecurityException)
        {
            // Security restriction, continue with size = 0
        }

        return new DroppedItem
        {
            FullPath = path,
            IsDirectory = isDirectory,
            Size = size,
            Category = category
        };
    }
}
