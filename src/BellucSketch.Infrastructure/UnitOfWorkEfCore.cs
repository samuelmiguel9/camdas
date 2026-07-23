using BellucSketch.Application.Abstractions;
using BellucSketch.Infrastructure.Persistence;

namespace BellucSketch.Infrastructure;

public sealed class UnitOfWorkEfCore(BellucSketchDbContext dbContext) : IUnitOfWork
{
    public Task SalvarAlteracoesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
