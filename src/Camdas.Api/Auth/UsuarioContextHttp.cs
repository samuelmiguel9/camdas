using System.IdentityModel.Tokens.Jwt;
using Camdas.Application.Abstractions;

namespace Camdas.Api.Auth;

/// <summary>
/// Lê o usuário autenticado a partir da claim "sub" do JWT da requisição atual.
/// </summary>
public sealed class UsuarioContextHttp(IHttpContextAccessor httpContextAccessor) : IUsuarioContext
{
    public Guid UsuarioId
    {
        get
        {
            var valor = httpContextAccessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(valor) || !Guid.TryParse(valor, out var usuarioId))
                throw new InvalidOperationException("Requisição sem usuário autenticado (claim 'sub' ausente).");

            return usuarioId;
        }
    }
}
