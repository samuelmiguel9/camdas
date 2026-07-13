using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Camdas.Api.Auth;

public sealed class JwtTokenGenerator(IOptions<JwtOptions> opcoes) : IJwtTokenGenerator
{
    public string GerarToken(Guid usuarioId, string nome)
    {
        var opcao = opcoes.Value;
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opcao.Chave));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, usuarioId.ToString()),
            new(ClaimTypes.Name, nome),
        ];

        var token = new JwtSecurityToken(
            issuer: opcao.Emissor,
            audience: opcao.Audiencia,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(opcao.ExpiracaoMinutos),
            signingCredentials: credenciais);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
