namespace Camdas.Mobile.Services;

/// <summary>
/// Na abertura da tela de login, tenta descobrir automaticamente qual dos endereços salvos (casa,
/// trabalho, etc.) responde na rede atual — evita que o usuário precise editar/recompilar o app toda
/// vez que troca de rede. Se nenhum responder, a tela de login deve pedir o IP novo e um nome, e
/// chamar <see cref="ConfigurarNovoEnderecoAsync"/>, que salva automaticamente para as próximas vezes.
/// </summary>
public sealed class ResolvedorEnderecoApi(ConfiguracaoApi configuracao, IArmazenamentoEnderecosApi armazenamento)
{
    private static readonly TimeSpan TimeoutPorEndereco = TimeSpan.FromSeconds(2);

    /// <summary>Tenta cada endereço salvo (ativo primeiro) e adota o primeiro que responder.</summary>
    public async Task<bool> ResolverAsync(CancellationToken ct = default)
    {
        foreach (var endereco in armazenamento.Listar())
        {
            if (await RespondeAsync(endereco.BaseUrl, ct))
            {
                configuracao.BaseUrl = endereco.BaseUrl;
                armazenamento.Salvar(endereco);
                return true;
            }
        }

        return false;
    }

    /// <summary>Salva um endereço novo informado manualmente pelo usuário e passa a usá-lo.</summary>
    public Task ConfigurarNovoEnderecoAsync(string nome, string baseUrl)
    {
        if (!baseUrl.Contains("://", StringComparison.Ordinal))
            baseUrl = "http://" + baseUrl;
        if (!baseUrl.EndsWith('/'))
            baseUrl += "/";

        var endereco = new EnderecoApi(nome, baseUrl);
        armazenamento.Salvar(endereco);
        configuracao.BaseUrl = baseUrl;
        return Task.CompletedTask;
    }

    private static async Task<bool> RespondeAsync(string baseUrl, CancellationToken ct)
    {
        try
        {
            using var cliente = new HttpClient { Timeout = TimeoutPorEndereco };
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeoutPorEndereco);
            using var resposta = await cliente.GetAsync(new Uri(new Uri(baseUrl), "health"), cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
