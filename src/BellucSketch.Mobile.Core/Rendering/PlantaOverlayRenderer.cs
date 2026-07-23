using BellucSketch.Contracts;
using SkiaSharp;

namespace BellucSketch.Mobile.Rendering;

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
        if (PodeDesenhar(imagemBase))
            canvas.DrawBitmap(imagemBase, 0, 0);

        if (imagensRasterPorCamada is null)
            return;

        foreach (var camada in camadas)
        {
            if (!camada.Visivel)
                continue;

            if (!imagensRasterPorCamada.TryGetValue(camada.Id, out var imagemCamada) || !PodeDesenhar(imagemCamada))
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

    /// <summary>
    /// No SkiaSharp 3.x, <c>SKCanvas.DrawBitmap</c> empacota o bitmap num <c>SKImage</c> internamente
    /// (sk_image_new_from_bitmap). Se o bitmap já foi liberado (Handle nulo) ou está sem pixels
    /// alocados, essa chamada nativa faz null pointer dereference e derruba o app com SIGSEGV — sem
    /// exceção gerenciável, então tem que ser barrado ANTES de desenhar. Acontecia em redesenhos
    /// disparados por transições de foco/navegação, quando um bitmap era trocado/liberado em paralelo.
    /// </summary>
    public static bool PodeDesenhar(SKBitmap? bitmap)
    {
        if (bitmap is null)
            return false;

        try
        {
            return bitmap.Handle != IntPtr.Zero
                && bitmap is { Width: > 0, Height: > 0 }
                && bitmap.ReadyToDraw;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}
