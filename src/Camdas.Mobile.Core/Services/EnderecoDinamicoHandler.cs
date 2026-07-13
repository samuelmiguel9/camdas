namespace Camdas.Mobile.Services;

/// <summary>
/// Reescreve a autoridade (host:porta) de cada requisição para o valor atual de
/// <see cref="ConfiguracaoApi.BaseUrl"/> — em vez do valor capturado quando o <see cref="HttpClient"/>
/// foi construído. Isso permite trocar de servidor em tempo de execução (ex.: <see cref="ResolvedorEnderecoApi"/>
/// resolvendo um novo IP na tela de login) sem precisar recriar o HttpClient/ViewModel já instanciado pelo DI.
/// </summary>
public sealed class EnderecoDinamicoHandler(ConfiguracaoApi configuracao) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is not null)
        {
            var baseUri = new Uri(configuracao.BaseUrl);
            request.RequestUri = new Uri(baseUri, request.RequestUri.PathAndQuery);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
