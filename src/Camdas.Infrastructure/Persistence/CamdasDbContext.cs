using Camdas.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Camdas.Infrastructure.Persistence;

/// <summary>
/// Único DbContext da aplicação. Apenas os agregados raiz (<see cref="Projeto"/>, <see cref="Planta"/>)
/// e a entidade independente <see cref="HistoricoAlteracao"/> têm DbSet próprio — Camada é acessada
/// exclusivamente através da navegação de Planta, respeitando o limite do agregado (mesma regra
/// refletida em IPlantaRepository na camada de Aplicação).
/// </summary>
public sealed class CamdasDbContext : DbContext
{
    public CamdasDbContext(DbContextOptions<CamdasDbContext> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Projeto> Projetos => Set<Projeto>();
    public DbSet<Planta> Plantas => Set<Planta>();
    public DbSet<HistoricoAlteracao> HistoricosDeAlteracao => Set<HistoricoAlteracao>();
    public DbSet<EdicaoPendenteCamada> EdicoesPendentesCamada => Set<EdicaoPendenteCamada>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CamdasDbContext).Assembly);

        // Todo Id é um Guid atribuído pela própria entidade (Entity.Id) na criação — nunca gerado
        // pelo banco. Sem isso, o EF Core assume por convenção que uma chave Guid "não vazia" pode
        // já existir no banco (ValueGeneratedOnAdd): uma entidade filha nova, descoberta via grafo
        // depois que o pai já foi carregado como Unchanged (ex.: adicionar uma Camada numa Planta já
        // persistida), acaba marcada como Modified em vez de Added — gerando um UPDATE indevido (0
        // linhas afetadas / DbUpdateConcurrencyException) em vez do INSERT esperado.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var idProperty = entityType.FindProperty("Id");
            if (idProperty is not null && idProperty.ClrType == typeof(Guid))
                idProperty.ValueGenerated = ValueGenerated.Never;
        }

        base.OnModelCreating(modelBuilder);
    }
}
