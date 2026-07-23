using BellucSketch.Mobile.Services;

namespace BellucSketch.Mobile.Platforms.Services;

/// <summary>
/// Guarda o rascunho no diretório de dados do próprio app (<c>FileSystem.AppDataDirectory</c>) —
/// some sozinho se o app for desinstalado, e não precisa de permissão nenhuma (diferente da
/// galeria), já que é uma pasta privada do app.
/// </summary>
public sealed class ArmazenamentoRascunhoArquivo : IArmazenamentoRascunho
{
    private static string CaminhoDoArquivo(Guid camadaId) =>
        Path.Combine(FileSystem.AppDataDirectory, $"rascunho_camada_{camadaId}.png");

    public async Task SalvarAsync(Guid camadaId, byte[] pngBytes, CancellationToken ct = default) =>
        await File.WriteAllBytesAsync(CaminhoDoArquivo(camadaId), pngBytes, ct);

    public async Task<byte[]?> CarregarAsync(Guid camadaId, CancellationToken ct = default)
    {
        var caminho = CaminhoDoArquivo(camadaId);
        return File.Exists(caminho) ? await File.ReadAllBytesAsync(caminho, ct) : null;
    }

    public void Remover(Guid camadaId)
    {
        var caminho = CaminhoDoArquivo(camadaId);
        if (File.Exists(caminho))
            File.Delete(caminho);
    }
}
