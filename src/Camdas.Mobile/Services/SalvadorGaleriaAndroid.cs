using Android.Content;
using Android.Provider;
using Camdas.Mobile.Services;

namespace Camdas.Mobile.Platforms.Services;

/// <summary>
/// Salva um PNG na galeria (pasta Pictures/Camdas) via MediaStore — API a partir do Android 10
/// (RelativePath só existe a partir daí; é a versão mínima realista de aparelho em uso hoje, ver
/// GUIA_INSTALACAO_ANDROID.md).
/// </summary>
public sealed class SalvadorGaleriaAndroid : ISalvadorGaleria
{
    public async Task SalvarPngAsync(byte[] pngBytes, string nomeArquivo, CancellationToken ct = default)
    {
        var contexto = Android.App.Application.Context;
        var resolver = contexto.ContentResolver
            ?? throw new InvalidOperationException("ContentResolver indisponível.");

        using var valores = new ContentValues();
        valores.Put(MediaStore.MediaColumns.DisplayName, nomeArquivo);
        valores.Put(MediaStore.MediaColumns.MimeType, "image/png");
        valores.Put(MediaStore.MediaColumns.RelativePath, "Pictures/Camdas");

        var uri = resolver.Insert(MediaStore.Images.Media.ExternalContentUri!, valores)
            ?? throw new InvalidOperationException("Não foi possível criar o arquivo na galeria.");

        await using var stream = resolver.OpenOutputStream(uri)
            ?? throw new InvalidOperationException("Não foi possível abrir o arquivo pra escrita.");
        await stream.WriteAsync(pngBytes, ct);
    }
}
