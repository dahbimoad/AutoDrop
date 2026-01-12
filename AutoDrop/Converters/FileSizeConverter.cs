using System.Globalization;
using System.Windows.Data;

namespace AutoDrop.Converters;

/// <summary>
/// Converts file size in bytes to a human-readable format.
/// </summary>
[ValueConversion(typeof(long), typeof(string))]
public sealed class FileSizeConverter : IValueConverter
{
    private static readonly string[] SizeUnits = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>
    /// Converts bytes to a human-readable string (static method for direct use).
    /// </summary>
    public static string Convert(long bytes)
    {
        if (bytes == 0)
        {
            return "0 B";
        }

        var unitIndex = 0;
        var size = (double)bytes;

        while (size >= 1024 && unitIndex < SizeUnits.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{size:N0} {SizeUnits[unitIndex]}"
            : $"{size:N1} {SizeUnits[unitIndex]}";
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long bytes)
        {
            return "0 B";
        }

        return Convert(bytes);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException("FileSizeConverter does not support ConvertBack.");
    }
}
