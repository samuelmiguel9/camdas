using Camdas.Contracts;
using Camdas.Mobile.Rendering;
using FluentAssertions;
using SkiaSharp;
using Xunit;

namespace Camdas.Mobile.Tests.Rendering;

public class PlantaOverlayRendererTests
{
    private static CamadaDto NovaCamada(bool visivel, double opacidade = 1.0) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Elétrica", visivel, false, 1, false, opacidade);

    [Fact]
    public void Deve_desenhar_imagem_base_antes_das_camadas()
    {
        using var bitmap = new SKBitmap(10, 10);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var imagemBase = new SKBitmap(10, 10);
        using (var canvasBase = new SKCanvas(imagemBase))
            canvasBase.Clear(new SKColor(0x00, 0xFF, 0x00));

        PlantaOverlayRenderer.Desenhar(canvas, [], imagemBase);

        bitmap.GetPixel(5, 5).Should().Be(new SKColor(0x00, 0xFF, 0x00));
    }

    [Fact]
    public void Deve_desenhar_traco_livre_da_camada_visivel_por_cima_da_imagem_base()
    {
        using var bitmap = new SKBitmap(10, 10);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var imagemBase = new SKBitmap(10, 10);
        using (var canvasBase = new SKCanvas(imagemBase))
            canvasBase.Clear(new SKColor(0x00, 0xFF, 0x00));

        var camada = NovaCamada(visivel: true);

        using var tracoCamada = new SKBitmap(10, 10);
        using (var canvasTraco = new SKCanvas(tracoCamada))
            canvasTraco.Clear(new SKColor(0x00, 0x00, 0xFF));

        var imagensPorCamada = new Dictionary<Guid, SKBitmap> { [camada.Id] = tracoCamada };

        PlantaOverlayRenderer.Desenhar(canvas, [camada], imagemBase, imagensPorCamada);

        bitmap.GetPixel(5, 5).Should().Be(new SKColor(0x00, 0x00, 0xFF));
    }

    [Fact]
    public void Nao_deve_desenhar_traco_livre_de_camada_oculta()
    {
        using var bitmap = new SKBitmap(10, 10);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var camadaOculta = NovaCamada(visivel: false);

        using var tracoCamada = new SKBitmap(10, 10);
        using (var canvasTraco = new SKCanvas(tracoCamada))
            canvasTraco.Clear(new SKColor(0x00, 0x00, 0xFF));

        var imagensPorCamada = new Dictionary<Guid, SKBitmap> { [camadaOculta.Id] = tracoCamada };

        PlantaOverlayRenderer.Desenhar(canvas, [camadaOculta], imagemBase: null, imagensPorCamada);

        bitmap.GetPixel(5, 5).Should().Be(SKColors.White);
    }

    [Fact]
    public void Deve_atenuar_traco_livre_conforme_opacidade_da_camada()
    {
        using var bitmap = new SKBitmap(10, 10);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var camada = NovaCamada(visivel: true, opacidade: 0.5);

        using var tracoCamada = new SKBitmap(10, 10);
        using (var canvasTraco = new SKCanvas(tracoCamada))
            canvasTraco.Clear(SKColors.Black);

        var imagensPorCamada = new Dictionary<Guid, SKBitmap> { [camada.Id] = tracoCamada };

        PlantaOverlayRenderer.Desenhar(canvas, [camada], imagemBase: null, imagensPorCamada);

        var pixel = bitmap.GetPixel(5, 5);
        pixel.Should().NotBe(SKColors.Black);
        pixel.Should().NotBe(SKColors.White);
    }
}
