using Camdas.Domain.Entities;

namespace Camdas.Application.Abstractions;

public interface IProjetoRepository
{
    Task<Projeto?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Projeto>> ListarAsync(CancellationToken cancellationToken);
    void Adicionar(Projeto projeto);
    void Remover(Projeto projeto);
}
