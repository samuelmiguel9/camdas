using System.Globalization;

namespace BellucSketch.Mobile.Converters;

/// <summary>
/// Converte um bool para um texto legível conforme <c>ConverterParameter="SeVerdadeiro;SeFalso"</c>.
/// </summary>
public sealed class BoolParaTextoConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool booleano || parameter is not string opcoes)
            return string.Empty;

        var partes = opcoes.Split(';');
        return booleano ? partes[0] : partes[1];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
