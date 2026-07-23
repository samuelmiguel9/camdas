using BellucSketch.Domain.Entities;

namespace BellucSketch.Application.Abstractions;

public interface IHistoricoRepository
{
    void Adicionar(HistoricoAlteracao historico);
    Task<IReadOnlyList<HistoricoAlteracao>> ListarPorPlantaAsync(Guid plantaId, CancellationToken cancellationToken);
}
