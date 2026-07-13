using Camdas.Application.Abstractions;
using Camdas.Infrastructure.Persistence;

namespace Camdas.Infrastructure;

public sealed class UnitOfWorkEfCore(CamdasDbContext dbContext) : IUnitOfWork
{
    public Task SalvarAlteracoesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
