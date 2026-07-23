namespace BellucSketch.Application.Abstractions;

/// <summary>
/// Converte a primeira página de um PDF em uma imagem (PNG) usada como fundo para overlay de
/// camadas/cotas — o app não desenha sobre PDF diretamente, sempre sobre uma imagem raster.
/// </summary>
public interface IConversorPdfParaImagem
{
    Task<Stream> ConverterPrimeiraPaginaAsync(Stream pdf, CancellationToken cancellationToken);
}
