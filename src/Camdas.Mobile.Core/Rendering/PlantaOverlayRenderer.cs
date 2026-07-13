using Camdas.Contracts;
using SkiaSharp;

namespace Camdas.Mobile.Rendering;

/// <summary>
/// Desenha a imagem base da planta e, por cima, o traço livre (raster) de cada camada visível, na
/// ordem das camadas. Lógica pura (sem dependência de MAUI/Android) — testável isoladamente com
/// SkiaSharp em qualquer host .NET. Camadas ocultas (<see cref="CamadaDto.Visivel"/> falso) não têm
/// o traço livre desenhado.
/// </summary>
public static class PlantaOverlayRenderer
{
    public static void Desenhar(
        SKCanvas canvas,
        IReadOnlyList<CamadaDto> camadas,
        SKBitmap? imagemBase = null,
        IReadOnlyDictionary<Guid, SKBitmap>? imagensRasterPorCamada = null)
    {
        if (imagemBase is not null)
            canvas.DrawBitmap(imagemBase, 0, 0);

        if (imagensRasterPorCamada is null)
            return;

        foreach (var camada in camadas)
        {
            if (!camada.Visivel)
                continue;

            if (!imagensRasterPorCamada.TryGetValue(camada.Id, out var imagemCamada))
                continue;

            if (camada.Opacidade >= 0.999)
            {
                canvas.DrawBitmap(imagemCamada, 0, 0);
            }
            else
            {
                using var paint = new SKPaint { Color = new SKColor(255, 255, 255, (byte)Math.Clamp(camada.Opacidade * 255, 0, 255)) };
                canvas.DrawBitmap(imagemCamada, 0, 0, paint);
            }
        }
    }
}
