using Camdas.Application.Abstractions;
using Camdas.Domain.Entities;
using Camdas.Domain.Enums;
using Camdas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Camdas.Infrastructure.Repositories;

public sealed class EdicaoPendenteRepositoryEfCore(CamdasDbContext dbContext) : IEdicaoPendenteRepository
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
