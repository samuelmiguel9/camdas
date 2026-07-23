using BellucSketch.Application.Abstractions;
using BellucSketch.Domain.Entities;
using BellucSketch.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BellucSketch.Infrastructure.Repositories;

public sealed class ProjetoRepositoryEfCore(BellucSketchDbContext dbContext) : IProjetoRepository
{
    public Task<Projeto?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Projetos.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Projeto>> ListarAsync(CancellationToken cancellationToken) =>
        await dbContext.Projetos.OrderBy(p => p.Nome).ToListAsync(cancellationToken);

    public void Adicionar(Projeto projeto) => dbContext.Projetos.Add(projeto);

    public void Remover(Projeto projeto) => dbContext.Projetos.Remove(projeto);
}
