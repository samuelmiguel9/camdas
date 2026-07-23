using BellucSketch.Application.Abstractions;
using PDFtoImage;
using SkiaSharp;

namespace BellucSketch.Infrastructure.Import;

/// <summary>
/// Usa PDFium (via PDFtoImage/SkiaSharp) para renderizar a primeira página do PDF como PNG.
/// </summary>
public sealed class ConversorPdfParaImagemPdfium : IConversorPdfParaImagem
{
    /// <summary>
    /// DPI padrão do PDFtoImage é 72 (o "ponto" nativo do PDF) — dá uma imagem baixa o bastante pra
    /// ficar visivelmente borrada assim que o usuário dá zoom no app (bug reportado). 450 (antes 300):
    /// pra uma folha A1 (tamanho comum de planta impressa) qualquer um dos dois já passa do teto de
    /// nitidez que o celular aplica no carregamento (ver BitmapDecodificacao.DimensaoMaximaPadrao), mas
    /// pra folhas menores (A3/A4) 300 DPI mal alcançava esse teto — subimos os dois juntos (pedido do
    /// usuário: "melhore a resolução de qualquer entrada") pra folha pequena também aproveitar o
    /// espaço extra de zoom sem pixelizar.
    /// </summary>
    private const int DpiConversao = 450;

    public Task<Stream> ConverterPrimeiraPaginaAsync(Stream pdf, CancellationToken cancellationToken)
    {
        if (pdf.CanSeek)
            pdf.Position = 0;

        using var bitmap = Conversion.ToImage(pdf, page: 0, options: new RenderOptions(Dpi: DpiConversao));
        using var imagem = SKImage.FromBitmap(bitmap);
        using var dadosPng = imagem.Encode(SKEncodedImageFormat.Png, 100);

        var saida = new MemoryStream();
        dadosPng.SaveTo(saida);
        saida.Position = 0;

        return Task.FromResult<Stream>(saida);
    }
}
