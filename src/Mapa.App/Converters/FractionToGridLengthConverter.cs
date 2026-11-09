using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Mapa.App.Converters;

/// <summary>
/// Converts a 0..1 fraction into a Star GridLength, so a two-column Grid (bar | remainder)
/// renders a proportional bar without any pixel-width math. Pass ConverterParameter="Invert"
/// for the complementary (remainder) column.
/// </summary>
public sealed class FractionToGridLengthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var fraction = value is double d ? Math.Clamp(d, 0.0, 1.0) : 0.0;
        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
            fraction = 1.0 - fraction;

        return new GridLength(fraction, GridUnitType.Star);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
