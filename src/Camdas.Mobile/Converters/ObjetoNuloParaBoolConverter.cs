using System.Globalization;

namespace Camdas.Mobile.Converters;

/// <summary>
/// Retorna true se o valor for nulo. Passe <c>ConverterParameter="inverso"</c> para inverter (true
/// quando o valor NÃO for nulo) — usado para mostrar/esconder seções da tela conforme um objeto
/// (ex.: a revisão atual) existir ou não.
/// </summary>
public sealed class ObjetoNuloParaBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var ehNulo = value is null;
        return string.Equals(parameter as string, "inverso", StringComparison.OrdinalIgnoreCase) ? !ehNulo : ehNulo;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
