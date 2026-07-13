using Camdas.Application.Abstractions;
using Camdas.Domain.Entities;
using Camdas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Camdas.Infrastructure.Repositories;

public sealed class HistoricoRepositoryEfCore(CamdasDbContext dbContext) : IHistoricoRepository
{
    public void Adicionar(HistoricoAlteracao historico) => dbContext.HistoricosDeAlteracao.Add(historico);

    public async Task<IReadOnlyList<HistoricoAlteracao>> ListarPorPlantaAsync(Guid plantaId, CancellationToken cancellationToken) =>
        await dbContext.HistoricosDeAlteracao
            .Where(h => h.PlantaId == plantaId)
            .OrderByDescending(h => h.DataHora)
            .ToListAsync(cancellationToken);
}
