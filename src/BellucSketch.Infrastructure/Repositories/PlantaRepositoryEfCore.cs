using BellucSketch.Application.Abstractions;
using BellucSketch.Domain.Entities;
using BellucSketch.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BellucSketch.Infrastructure.Repositories;

/// <summary>
/// Sempre carrega o agregado Planta completo (com Camadas) — não existe uma forma de carregar
/// Planta "parcialmente" pelo repositório, para não arriscar operar sobre um agregado incompleto e
/// violar suas invariantes.
/// </summary>
public sealed class PlantaRepositoryEfCore(BellucSketchDbContext dbContext) : IPlantaRepository
{
    public Task<Planta?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        ConsultaComAgregadoCompleto().SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Planta>> ListarPorProjetoAsync(Guid projetoId, CancellationToken cancellationToken) =>
        await ConsultaComAgregadoCompleto()
            .Where(p => p.ProjetoId == projetoId)
            .OrderByDescending(p => p.DataImportacao)
            .ToListAsync(cancellationToken);

    public void Adicionar(Planta planta) => dbContext.Plantas.Add(planta);

    public void Remover(Planta planta) => dbContext.Plantas.Remove(planta);

    private IQueryable<Planta> ConsultaComAgregadoCompleto() =>
        dbContext.Plantas.Include(p => p.Camadas);
}
