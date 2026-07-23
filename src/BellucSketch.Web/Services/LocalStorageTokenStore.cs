using BellucSketch.Mobile.Services;
using Microsoft.JSInterop;

namespace BellucSketch.Web.Services;

/// <summary>
/// Implementação de <see cref="ITokenStore"/> para o navegador — guarda o JWT no localStorage via
/// JS interop. Equivalente ao TokenStoreSecureStorage do app Android, só que sem Keystore.
/// </summary>
public sealed class LocalStorageTokenStore(IJSRuntime jsRuntime) : ITokenStore
{
    private const string Chave = "camdas_token";

    public async Task<string?> ObterTokenAsync() =>
        await jsRuntime.InvokeAsync<string?>("localStorage.getItem", Chave);

    public async Task SalvarTokenAsync(string token) =>
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", Chave, token);

    public async Task LimparAsync() =>
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", Chave);
}
