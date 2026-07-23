using BellucSketch.Domain.Entities;

namespace BellucSketch.Application.Abstractions;

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExisteComEmailAsync(string email, CancellationToken cancellationToken);

    void Adicionar(Usuario usuario);
}
