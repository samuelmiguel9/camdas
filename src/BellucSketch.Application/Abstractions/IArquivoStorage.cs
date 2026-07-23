namespace BellucSketch.Application.Abstractions;

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

    /// <summary>
    /// Apaga o arquivo salvo em <paramref name="caminho"/> — usado sempre que uma referência (imagem
    /// de camada, arquivo original da planta) é substituída ou deixa de existir, senão o arquivo
    /// antigo fica órfão no armazenamento pra sempre (ninguém mais aponta pra ele, mas ele continua
    /// ocupando espaço). Não deve lançar exceção se o arquivo já não existir (limpeza é best-effort:
    /// nunca deve derrubar uma operação que já terminou de gravar as mudanças de verdade no banco).
    /// </summary>
    Task ExcluirAsync(string caminho, CancellationToken cancellationToken);
}
