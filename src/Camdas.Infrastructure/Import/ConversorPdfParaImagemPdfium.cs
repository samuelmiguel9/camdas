using Camdas.Application.Abstractions;
using PDFtoImage;
using SkiaSharp;

namespace Camdas.Infrastructure.Import;

/// <summary>
/// Usa PDFium (via PDFtoImage/SkiaSharp) para renderizar a primeira página do PDF como PNG.
/// </summary>
public sealed class ConversorPdfParaImagemPdfium : IConversorPdfParaImagem
{
    /// <summary>
    /// DPI padrão do PDFtoImage é 72 (o "ponto" nativo do PDF) — dá uma imagem baixa o bastante pra
    /// ficar visivelmente borrada assim que o usuário dá zoom no app (bug reportado). 300 é a
    /// resolução usual de impressão/scan de qualidade: pra uma folha A1 (o tamanho comum de planta
    /// impressa), já rende algo em torno de 9900x7016px — bem mais espaço pra dar zoom sem
    /// pixelizar, sem passar de uns poucos MB de PNG.
    /// </summary>
    private const int DpiConversao = 300;

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
