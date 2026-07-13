using Camdas.Domain.Entities;

namespace Camdas.Application.Abstractions;

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExisteComEmailAsync(string email, CancellationToken cancellationToken);

    void Adicionar(Usuario usuario);
}
