using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BellucSketch.Infrastructure.Persistence;

/// <summary>
/// Permite gerar/aplicar migrations via `dotnet ef` sem depender do projeto Api (ainda não existia
/// quando este passo foi implementado). A Api (Fase 4) configura a connection string real via
/// appsettings/variáveis de ambiente — aqui usamos um valor de desenvolvimento local, só para
/// design-time (nunca é usado em runtime).
/// </summary>
public sealed class BellucSketchDbContextFactory : IDesignTimeDbContextFactory<BellucSketchDbContext>
{
    public BellucSketchDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CAMDAS_CONNECTION_STRING")
            ?? "Host=localhost;Database=camdas;Username=camdas;Password=camdas";

        var optionsBuilder = new DbContextOptionsBuilder<BellucSketchDbContext>()
            .UseNpgsql(connectionString);

        return new BellucSketchDbContext(optionsBuilder.Options);
    }
}
