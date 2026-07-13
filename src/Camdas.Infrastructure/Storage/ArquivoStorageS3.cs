using Amazon.S3;
using Amazon.S3.Model;
using Camdas.Application.Abstractions;

namespace Camdas.Infrastructure.Storage;

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
        }, cancellationToken);

        return chave;
    }

    public async Task<Stream> AbrirAsync(string caminho, CancellationToken cancellationToken)
    {
        var resposta = await cliente.GetObjectAsync(bucket, caminho, cancellationToken);
        return resposta.ResponseStream;
    }
}
