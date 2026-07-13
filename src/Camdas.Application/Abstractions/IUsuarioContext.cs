namespace Camdas.Application.Abstractions;

/// <summary>
/// Identidade do usuário autenticado na requisição atual (populada pela Api a partir das claims do
/// JWT). Os casos de uso não confiam no perfil vindo do token para autorizar ações sensíveis —
/// buscam o Usuario atualizado via <see cref="IUsuarioRepository"/> para refletir eventuais
/// mudanças de perfil/desativação ocorridas após o token ser emitido.
/// </summary>
public interface IUsuarioContext
{
    Guid UsuarioId { get; }
}
