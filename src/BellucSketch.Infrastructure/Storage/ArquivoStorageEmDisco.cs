using BellucSketch.Application.Abstractions;

namespace BellucSketch.Infrastructure.Storage;

/// <summary>
/// Salva o arquivo em um diretório raiz configurável — pode ser um disco local do servidor onde a
/// Api roda, ou um caminho de rede (UNC) mapeado para um servidor de arquivos da intranet, de forma
/// transparente para quem consome <see cref="IArquivoStorage"/>.
/// </summary>
public sealed class ArquivoStorageEmDisco : IArquivoStorage
{
    private readonly string _diretorioRaiz;

    public ArquivoStorageEmDisco(string diretorioRaiz)
    {
        _diretorioRaiz = diretorioRaiz;
        Directory.CreateDirectory(_diretorioRaiz);
    }

    public async Task<string> SalvarAsync(string nomeArquivo, Stream conteudo, CancellationToken cancellationToken)
    {
        var nomeUnico = $"{Guid.NewGuid()}_{Path.GetFileName(nomeArquivo)}";
        var caminhoCompleto = Path.Combine(_diretorioRaiz, nomeUnico);

        if (conteudo.CanSeek)
            conteudo.Position = 0;

        await using var arquivoDestino = File.Create(caminhoCompleto);
        await conteudo.CopyToAsync(arquivoDestino, cancellationToken);

        return caminhoCompleto;
    }

    public Task<Stream> AbrirAsync(string caminho, CancellationToken cancellationToken)
    {
        var caminhoNormalizado = ValidarDentroDaRaiz(caminho);
        Stream stream = File.OpenRead(caminhoNormalizado);
        return Task.FromResult(stream);
    }

    public Task ExcluirAsync(string caminho, CancellationToken cancellationToken)
    {
        // File.Delete não lança se o arquivo já não existir — cumpre o contrato "best-effort" da
        // interface sem tratamento extra.
        File.Delete(ValidarDentroDaRaiz(caminho));
        return Task.CompletedTask;
    }

    private string ValidarDentroDaRaiz(string caminho)
    {
        var raizNormalizada = Path.GetFullPath(_diretorioRaiz);
        var caminhoNormalizado = Path.GetFullPath(caminho);

        if (!caminhoNormalizado.StartsWith(raizNormalizada, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Caminho de arquivo fora do diretório raiz de armazenamento.");

        return caminhoNormalizado;
    }
}
