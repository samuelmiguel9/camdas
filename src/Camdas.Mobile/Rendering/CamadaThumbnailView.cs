using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Camdas.Mobile.Views;

/// <summary>
/// Miniatura de uma camada pro painel estilo Photoshop: desenha um xadrez cinza (indica
/// transparência, igual à miniatura de camada do Photoshop) e, por cima, o traço da camada
/// (<see cref="ImagensPorCamada"/>[<see cref="CamadaId"/>]) redimensionado mantendo proporção.
/// </summary>
public sealed class CamadaThumbnailView : SKCanvasView
{
    public static readonly BindableProperty CamadaIdProperty = BindableProperty.Create(
        nameof(CamadaId), typeof(Guid?), typeof(CamadaThumbnailView),
        defaultValue: null,
        propertyChanged: (bindable, _, _) => ((CamadaThumbnailView)bindable).InvalidateSurface());

    public static readonly BindableProperty ImagensPorCamadaProperty = BindableProperty.Create(
        nameof(ImagensPorCamada), typeof(IDictionary<Guid, SKBitmap>), typeof(CamadaThumbnailView),
        defaultValue: null,
        propertyChanged: (bindable, _, _) => ((CamadaThumbnailView)bindable).InvalidateSurface());

    public Guid? CamadaId
    {
        get => (Guid?)GetValue(CamadaIdProperty);
        set => SetValue(CamadaIdProperty, value);
    }

    public IDictionary<Guid, SKBitmap>? ImagensPorCamada
    {
        get => (IDictionary<Guid, SKBitmap>?)GetValue(ImagensPorCamadaProperty);
        set => SetValue(ImagensPorCamadaProperty, value);
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);

        var canvas = e.Surface.Canvas;
        var info = e.Info;
        DesenharXadrez(canvas, info);

        if (CamadaId is not { } id || ImagensPorCamada is null || !ImagensPorCamada.TryGetValue(id, out var bitmap))
            return;

        var escala = Math.Min((float)info.Width / bitmap.Width, (float)info.Height / bitmap.Height);
        var largura = bitmap.Width * escala;
        var altura = bitmap.Height * escala;
        var destino = new SKRect(
            (info.Width - largura) / 2f, (info.Height - altura) / 2f,
            (info.Width + largura) / 2f, (info.Height + altura) / 2f);

        using var paint = new SKPaint { FilterQuality = SKFilterQuality.Medium };
        canvas.DrawBitmap(bitmap, destino, paint);
    }

    private static void DesenharXadrez(SKCanvas canvas, SKImageInfo info)
    {
        const int tamanhoQuadro = 6;
        using var claro = new SKPaint { Color = new SKColor(0x5A, 0x5A, 0x5A) };
        using var escuro = new SKPaint { Color = new SKColor(0x40, 0x40, 0x40) };

        canvas.Clear(claro.Color);
        for (var y = 0; y * tamanhoQuadro < info.Height; y++)
        for (var x = 0; x * tamanhoQuadro < info.Width; x++)
        {
            if ((x + y) % 2 == 0)
                continue;

            canvas.DrawRect(
                new SKRect(x * tamanhoQuadro, y * tamanhoQuadro, (x + 1) * tamanhoQuadro, (y + 1) * tamanhoQuadro),
                escuro);
        }
    }
}
