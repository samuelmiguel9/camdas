using System.Globalization;

namespace BellucSketch.Mobile.Converters;

/// <summary>
/// Compara o Id de uma camada (primeiro valor da MultiBinding) com o Id da camada ativa (segundo
/// valor — pode não vir, se não houver camada ativa) e devolve uma cor de destaque quando batem —
/// usado pra marcar visualmente qual camada é a que está sendo editada na lista (pedido: "não está
/// visual" qual é a atual).
/// </summary>
public sealed class CamadaAtivaParaCorConverter : IMultiValueConverter
{
    private static readonly Color CorDestaque = Color.FromArgb("#3A5C8A");

    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is [Guid camadaId, Guid camadaAtivaId] && camadaId == camadaAtivaId)
            return CorDestaque;

        return Colors.Transparent;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
