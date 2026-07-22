namespace Camdas.Mobile.Services;

/// <summary>
/// Endereço fixo do servidor Camdas.Api, hospedado no Render — o app rodou contra a intranet numa
/// fase anterior (daí existir esse endereço configurável), mas isso foi abandonado: hoje só existe
/// esse servidor na nuvem, então não há mais o que resolver/perguntar em tempo de execução.
/// </summary>
public sealed class ConfiguracaoApi
{
    public string BaseUrl { get; } = "https://camdas-api-gb9z.onrender.com/";
}
