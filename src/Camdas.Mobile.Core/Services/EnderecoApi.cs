namespace Camdas.Mobile.Services;

/// <summary>
/// Um endereço salvo do servidor Camdas.Api, identificado por um nome dado pelo usuário
/// (ex.: "Trabalho Starlink", "Casa").
/// </summary>
public sealed record EnderecoApi(string Nome, string BaseUrl);
