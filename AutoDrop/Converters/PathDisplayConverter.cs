using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace AutoDrop.Converters;

/// <summary>
/// Converts a full path into formatted display with parent path muted and folder name normal.
/// Returns an Inline collection for use in TextBlock.
/// </summary>
public sealed class PathParentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
            return string.Empty;

        try
        {
            var normalizedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var parentPath = Path.GetDirectoryName(normalizedPath);
            
            if (string.IsNullOrEmpty(parentPath))
                return string.Empty;

            return parentPath + Path.DirectorySeparatorChar;
        }
        catch
        {
            return string.Empty;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Extracts just the folder name from a full path.
/// </summary>
public sealed class PathFolderNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
            return string.Empty;

        try
        {
            var normalizedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetFileName(normalizedPath);
        }
        catch
        {
            return path;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Extracts the parent folder name from a file's full path.
/// Example: C:\Users\Dahbi\Downloads\file.txt → Downloads
/// </summary>
public sealed class PathToParentFolderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
            return string.Empty;

        try
        {
            var directoryPath = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directoryPath))
                return string.Empty;

            // Get just the folder name (last segment)
            return Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }
        catch
        {
            return path;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
