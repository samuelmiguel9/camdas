using Camdas.Domain.Entities;

namespace Camdas.Application.Abstractions;

public interface IHistoricoRepository
{
    void Adicionar(HistoricoAlteracao historico);
    Task<IReadOnlyList<HistoricoAlteracao>> ListarPorPlantaAsync(Guid plantaId, CancellationToken cancellationToken);
}
