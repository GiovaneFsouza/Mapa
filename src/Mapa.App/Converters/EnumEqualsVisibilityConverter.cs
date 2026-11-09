using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Mapa.App.Converters;

/// <summary>
/// Visibility toggle driven by comparing an enum-valued binding against ConverterParameter
/// (the enum member's name). Used to show the regional-quantity breakdown only in Quantidade mode.
/// </summary>
public sealed class EnumEqualsVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is not string paramStr)
            return Visibility.Collapsed;

        var matches = string.Equals(value.ToString(), paramStr, StringComparison.OrdinalIgnoreCase);
        return matches ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
