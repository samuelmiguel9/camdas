using BellucSketch.Mobile.Services;

namespace BellucSketch.Mobile.Platforms.Services;

/// <summary>
/// Implementação concreta de <see cref="ITokenStore"/> usando o Keystore do Android via
/// <c>SecureStorage</c> do MAUI Essentials — por isso vive aqui (projeto net8.0-android), não em
/// BellucSketch.Mobile.Core (biblioteca plain .NET, sem acesso a APIs de plataforma).
/// </summary>
public sealed class TokenStoreSecureStorage : ITokenStore
{
    private const string Chave = "camdas_jwt_token";

    public async Task<string?> ObterTokenAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(Chave);
        }
        catch (Exception)
        {
            // Chave do Android Keystore corrompida/invalidada (comum em aparelhos Samsung após
            // reinstalação) — limpa e trata como "sem token salvo" em vez de propagar o erro.
            SecureStorage.Default.RemoveAll();
            return null;
        }
    }

    public async Task SalvarTokenAsync(string token)
    {
        try
        {
            await SecureStorage.Default.SetAsync(Chave, token);
        }
        catch (Exception)
        {
            SecureStorage.Default.RemoveAll();
            await SecureStorage.Default.SetAsync(Chave, token);
        }
    }

    public Task LimparAsync()
    {
        SecureStorage.Default.Remove(Chave);
        return Task.CompletedTask;
    }
}
