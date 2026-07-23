using System.Globalization;

namespace BellucSketch.Mobile.Converters;

public sealed class InvertedBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool valorBooleano ? !valorBooleano : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool valorBooleano ? !valorBooleano : value;
}
