using BellucSketch.Application.Abstractions;
using BellucSketch.Domain.Entities;
using BellucSketch.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BellucSketch.Infrastructure.Repositories;

public sealed class HistoricoRepositoryEfCore(BellucSketchDbContext dbContext) : IHistoricoRepository
{
    public void Adicionar(HistoricoAlteracao historico) => dbContext.HistoricosDeAlteracao.Add(historico);

    public async Task<IReadOnlyList<HistoricoAlteracao>> ListarPorPlantaAsync(Guid plantaId, CancellationToken cancellationToken) =>
        await dbContext.HistoricosDeAlteracao
            .Where(h => h.PlantaId == plantaId)
            .OrderByDescending(h => h.DataHora)
            .ToListAsync(cancellationToken);
}
