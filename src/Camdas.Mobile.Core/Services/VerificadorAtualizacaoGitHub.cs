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
    /// Lida do arquivo VERSION (raiz do repo), embutido como recurso no build — mesma fonte usada
    /// pelo `ApplicationDisplayVersion` no Camdas.Mobile.csproj e pelo endpoint `/version` da Api.
    /// A tag do GitHub Release ainda precisa ser criada manualmente igual a este valor (`v` +
    /// conteúdo do VERSION) na hora de publicar, já que isso não depende do build.
    /// </summary>
    public static readonly string VersaoAtual = "v" + LerVersao();

    private static string LerVersao()
    {
        using var stream = typeof(VerificadorAtualizacaoGitHub).Assembly.GetManifestResourceStream("VERSION");
        using var leitor = new StreamReader(stream!);
        return leitor.ReadToEnd().Trim();
    }

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
