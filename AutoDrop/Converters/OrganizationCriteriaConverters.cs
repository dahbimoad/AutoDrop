using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AutoDrop.Models;
using Wpf.Ui.Controls;

namespace AutoDrop.Converters;

/// <summary>
/// Converts OrganizationCriteria to button Appearance based on selection.
/// </summary>
public sealed class CriteriaAppearanceConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not OrganizationCriteria currentCriteria || parameter is not string paramString)
            return ControlAppearance.Secondary;

        if (Enum.TryParse<OrganizationCriteria>(paramString, out var targetCriteria))
        {
            return currentCriteria == targetCriteria ? ControlAppearance.Primary : ControlAppearance.Secondary;
        }

        return ControlAppearance.Secondary;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts zero to true (for indeterminate progress bar).
/// </summary>
public sealed class ZeroToTrueConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
            return intValue == 0;
        if (value is double doubleValue)
            return doubleValue == 0;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
