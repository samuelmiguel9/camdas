namespace BellucSketch.Mobile.Services;

/// <summary>
/// Guarda uma cópia local do traço em andamento de uma camada, pra não perder o trabalho se o app
/// fechar antes de "Salvar camada" (que manda pro servidor). Implementação concreta usa o diretório
/// de dados do próprio app, por isso vive em BellucSketch.Mobile (acessa API de plataforma).
/// </summary>
public interface IArmazenamentoRascunho
{
    Task SalvarAsync(Guid camadaId, byte[] pngBytes, CancellationToken ct = default);
    Task<byte[]?> CarregarAsync(Guid camadaId, CancellationToken ct = default);
    void Remover(Guid camadaId);
}
