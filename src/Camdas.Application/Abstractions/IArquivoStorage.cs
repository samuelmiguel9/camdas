namespace Camdas.Application.Abstractions;

/// <summary>
/// Armazenamento do arquivo original importado (PDF ou imagem) no servidor de arquivos da
/// intranet. A implementação concreta (Infrastructure) decide o local físico/lógico.
/// </summary>
public interface IArquivoStorage
{
    /// <returns>Caminho/identificador pelo qual o arquivo pode ser recuperado depois.</returns>
    Task<string> SalvarAsync(string nomeArquivo, Stream conteudo, CancellationToken cancellationToken);

    /// <returns>Stream de leitura do conteúdo salvo em <paramref name="caminho"/>.</returns>
    Task<Stream> AbrirAsync(string caminho, CancellationToken cancellationToken);
}
