namespace BellucSketch.Api.Auth;

public interface IJwtTokenGenerator
{
    string GerarToken(Guid usuarioId, string nome);
}
