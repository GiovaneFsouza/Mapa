using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Mapa.App.Converters;

public sealed class HexColorToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(color);
            }
            catch (FormatException)
            {
                // fall through to default
            }
        }

        return new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
