namespace BellucSketch.Mobile.Services;

/// <summary>Salva um PNG na galeria de fotos do aparelho — implementação concreta usa a MediaStore
/// do Android, por isso vive em BellucSketch.Mobile (net8.0-android), não aqui.</summary>
public interface ISalvadorGaleria
{
    Task SalvarPngAsync(byte[] pngBytes, string nomeArquivo, CancellationToken ct = default);
}
