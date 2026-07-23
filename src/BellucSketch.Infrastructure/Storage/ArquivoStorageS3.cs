using Amazon.S3;
using Amazon.S3.Model;
using BellucSketch.Application.Abstractions;

namespace BellucSketch.Infrastructure.Storage;

/// <summary>
/// Salva o arquivo num bucket S3 (ou compatível com S3 — ex.: Supabase Storage, Cloudflare R2)
/// em vez de disco local. Necessário em hosts com disco efêmero (ex.: Render free), onde
/// <see cref="ArquivoStorageEmDisco"/> perderia os arquivos a cada reinício/deploy. O "caminho"
/// retornado por <see cref="SalvarAsync"/> é só a chave do objeto no bucket, não uma URL.
/// </summary>
public sealed class ArquivoStorageS3(IAmazonS3 cliente, string bucket) : IArquivoStorage
{
    public async Task<string> SalvarAsync(string nomeArquivo, Stream conteudo, CancellationToken cancellationToken)
    {
        var chave = $"{Guid.NewGuid()}_{Path.GetFileName(nomeArquivo)}";

        if (conteudo.CanSeek)
            conteudo.Position = 0;

        await cliente.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = chave,
            InputStream = conteudo,
            AutoCloseStream = false,
            // Por padrão o SDK sobe o arquivo em "chunks" assinados (streaming SigV4) — gateways
            // S3-compatíveis fora da AWS (Supabase Storage, MinIO, Cloudflare R2) costumam não
            // suportar isso e recusam com um 403 Forbidden genérico, sem detalhe (foi exatamente o
            // erro relatado). Desligando, o SDK assina o payload inteiro de uma vez, compatível com
            // qualquer S3-like.
            UseChunkEncoding = false,
        }, cancellationToken);

        return chave;
    }

    public async Task<Stream> AbrirAsync(string caminho, CancellationToken cancellationToken)
    {
        var resposta = await cliente.GetObjectAsync(bucket, caminho, cancellationToken);
        return resposta.ResponseStream;
    }

    // DeleteObjectAsync no S3 (e no Supabase Storage, que segue o mesmo contrato) é idempotente: não
    // lança erro se a chave já não existir, então não precisa de tratamento extra aqui pra cumprir o
    // "best-effort" pedido pela interface.
    public Task ExcluirAsync(string caminho, CancellationToken cancellationToken) =>
        cliente.DeleteObjectAsync(bucket, caminho, cancellationToken);
}
