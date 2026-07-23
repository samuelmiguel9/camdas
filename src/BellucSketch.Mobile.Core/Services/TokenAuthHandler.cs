using System.Net.Http.Headers;

namespace BellucSketch.Mobile.Services;

/// <summary>
/// Anexa automaticamente o Bearer token (se houver) em toda requisição feita pelo HttpClient da Api
/// — nenhum código de tela precisa lidar com o cabeçalho de autenticação manualmente.
/// </summary>
public sealed class TokenAuthHandler(ITokenStore tokenStore) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokenStore.ObterTokenAsync();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
