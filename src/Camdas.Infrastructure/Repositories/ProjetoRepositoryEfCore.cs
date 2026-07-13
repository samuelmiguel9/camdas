using Camdas.Application.Abstractions;
using Camdas.Domain.Entities;
using Camdas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Camdas.Infrastructure.Repositories;

public sealed class ProjetoRepositoryEfCore(CamdasDbContext dbContext) : IProjetoRepository
{
    public Task<Projeto?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Projetos.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Projeto>> ListarAsync(CancellationToken cancellationToken) =>
        await dbContext.Projetos.OrderBy(p => p.Nome).ToListAsync(cancellationToken);

    public void Adicionar(Projeto projeto) => dbContext.Projetos.Add(projeto);

    public void Remover(Projeto projeto) => dbContext.Projetos.Remove(projeto);
}
