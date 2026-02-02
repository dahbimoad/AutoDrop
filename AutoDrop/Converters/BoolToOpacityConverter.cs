using System.Globalization;
using System.Windows.Data;

namespace AutoDrop.Converters;

/// <summary>
/// Converts boolean to opacity value.
/// Parameter format: "trueValue|falseValue" (e.g., "1|0.4" means 1.0 when true, 0.4 when false).
/// Default: true = 1.0, false = 0.4.
/// </summary>
public sealed class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isTrue = value is bool boolValue && boolValue;
        
        // Parse parameter for custom values (format: "trueOpacity|falseOpacity")
        double trueOpacity = 1.0;
        double falseOpacity = 0.4;
        
        if (parameter is string paramStr && !string.IsNullOrEmpty(paramStr))
        {
            var parts = paramStr.Split('|');
            if (parts.Length >= 2)
            {
                _ = double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out trueOpacity);
                _ = double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out falseOpacity);
            }
        }
        
        return isTrue ? trueOpacity : falseOpacity;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Parse parameter for custom values
        double trueOpacity = 1.0;
        
        if (parameter is string paramStr && !string.IsNullOrEmpty(paramStr))
        {
            var parts = paramStr.Split('|');
            if (parts.Length >= 1)
            {
                _ = double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out trueOpacity);
            }
        }
        
        if (value is double opacity)
        {
            return Math.Abs(opacity - trueOpacity) < 0.01;
        }
        return false;
    }
}
