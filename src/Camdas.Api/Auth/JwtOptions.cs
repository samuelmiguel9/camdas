namespace Camdas.Api.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Chave { get; set; } = string.Empty;
    public string Emissor { get; set; } = string.Empty;
    public string Audiencia { get; set; } = string.Empty;
    public int ExpiracaoMinutos { get; set; } = 480;
}
