using System.Globalization;

namespace Camdas.Mobile.Converters;

public sealed class BytesParaImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is byte[] { Length: > 0 } bytes ? ImageSource.FromStream(() => new MemoryStream(bytes)) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
