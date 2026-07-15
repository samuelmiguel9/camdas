using System.Net.Http.Json;
using System.Text.Json;

namespace Camdas.Mobile.Services;

/// <summary>
/// Consulta <c>GET /repos/{Repositorio}/releases/latest</c> da API do GitHub (pública, sem
/// autenticação) e compara a tag do último release com <see cref="VersaoAtual"/>. Repositório fixo
/// porque este app só existe distribuído a partir dele — ver GUIA_INSTALACAO_ANDROID.md.
/// </summary>
public sealed class VerificadorAtualizacaoGitHub(HttpClient httpClient) : IVerificadorAtualizacao
{
    private const string Repositorio = "samuelmiguel9/camdas";

    /// <summary>
    /// Precisa ser atualizada junto com a tag do release e com `ApplicationDisplayVersion` no
    /// Camdas.Mobile.csproj a cada publicação — não há uma fonte única automática porque a tag do
    /// GitHub Release é decidida na hora de publicar, não no build.
    /// </summary>
    public const string VersaoAtual = "v1.5.0";

    public async Task<AtualizacaoDisponivel?> VerificarAsync(CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"repos/{Repositorio}/releases/latest");
            request.Headers.UserAgent.ParseAdd("Camdas.Mobile");
            using var resposta = await httpClient.SendAsync(request, ct);
            if (!resposta.IsSuccessStatusCode)
                return null;

            var json = await resposta.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var tag = json.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
            var url = json.TryGetProperty("html_url", out var urlEl) ? urlEl.GetString() : null;

            if (string.IsNullOrWhiteSpace(tag) || string.IsNullOrWhiteSpace(url) || tag == VersaoAtual)
                return null;

            return new AtualizacaoDisponivel(tag, url);
        }
        catch
        {
            // Sem internet, GitHub fora do ar, rate limit, JSON inesperado — checagem de atualização
            // é best-effort, nunca deve virar um erro visível pro usuário.
            return null;
        }
    }
}
