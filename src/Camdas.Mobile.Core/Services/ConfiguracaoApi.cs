namespace Camdas.Mobile.Services;

/// <summary>
/// Endereço do servidor Camdas.Api atualmente em uso. O valor inicial vem do endereço ativo salvo em
/// <see cref="IArmazenamentoEnderecosApi"/>; é atualizado em tempo real por <see cref="ResolvedorEnderecoApi"/>
/// (na tela de login) sempre que o dispositivo troca de rede (ex.: casa/trabalho), então nada aqui
/// deve ser lido apenas uma vez — <see cref="EnderecoDinamicoHandler"/> relê este valor a cada requisição.
/// </summary>
public sealed class ConfiguracaoApi
{
    public string BaseUrl { get; set; }

    public ConfiguracaoApi(IArmazenamentoEnderecosApi armazenamento)
    {
        var enderecos = armazenamento.Listar();
        BaseUrl = (armazenamento.ObterAtivo() ?? enderecos[0]).BaseUrl;
    }
}
