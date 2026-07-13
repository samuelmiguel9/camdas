using Camdas.Application.Abstractions;
using PDFtoImage;
using SkiaSharp;

namespace Camdas.Infrastructure.Import;

/// <summary>
/// Usa PDFium (via PDFtoImage/SkiaSharp) para renderizar a primeira página do PDF como PNG.
/// </summary>
public sealed class ConversorPdfParaImagemPdfium : IConversorPdfParaImagem
{
    public Task<Stream> ConverterPrimeiraPaginaAsync(Stream pdf, CancellationToken cancellationToken)
    {
        if (pdf.CanSeek)
            pdf.Position = 0;

        using var bitmap = Conversion.ToImage(pdf, page: 0);
        using var imagem = SKImage.FromBitmap(bitmap);
        using var dadosPng = imagem.Encode(SKEncodedImageFormat.Png, 100);

        var saida = new MemoryStream();
        dadosPng.SaveTo(saida);
        saida.Position = 0;

        return Task.FromResult<Stream>(saida);
    }
}
