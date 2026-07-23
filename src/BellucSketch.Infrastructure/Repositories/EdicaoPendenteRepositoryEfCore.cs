using BellucSketch.Application.Abstractions;
using BellucSketch.Domain.Entities;
using BellucSketch.Domain.Enums;
using BellucSketch.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BellucSketch.Infrastructure.Repositories;

public sealed class EdicaoPendenteRepositoryEfCore(BellucSketchDbContext dbContext) : IEdicaoPendenteRepository
{
    public void Adicionar(EdicaoPendenteCamada edicao) => dbContext.EdicoesPendentesCamada.Add(edicao);

    public async Task<EdicaoPendenteCamada?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.EdicoesPendentesCamada.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<EdicaoPendenteCamada>> ListarPendentesPorPlantaAsync(Guid plantaId, CancellationToken cancellationToken) =>
        await dbContext.EdicoesPendentesCamada
            .Where(e => e.PlantaId == plantaId && e.Status == StatusEdicaoPendente.Pendente)
            .OrderByDescending(e => e.DataSolicitacao)
            .ToListAsync(cancellationToken);
}
