using System.Globalization;
using System.Windows.Data;

namespace Mapa.App.Converters;

/// <summary>Classic WPF idiom for binding a group of RadioButtons to a single enum property.</summary>
public sealed class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is string s && value.ToString() == s;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is string s ? Enum.Parse(targetType, s) : Binding.DoNothing;
}
