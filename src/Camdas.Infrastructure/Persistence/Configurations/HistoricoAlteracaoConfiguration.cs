using Camdas.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Camdas.Infrastructure.Persistence.Configurations;

public sealed class HistoricoAlteracaoConfiguration : IEntityTypeConfiguration<HistoricoAlteracao>
{
    public void Configure(EntityTypeBuilder<HistoricoAlteracao> builder)
    {
        builder.ToTable("HistoricoAlteracoes");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.EntidadeTipo).IsRequired().HasMaxLength(100);
        builder.Property(h => h.EntidadeId).IsRequired();
        builder.Property(h => h.PlantaId);
        builder.Property(h => h.Acao).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(h => h.UsuarioId).IsRequired();
        builder.Property(h => h.DataHora).IsRequired();
        builder.Property(h => h.DadosAnterioresJson);
        builder.Property(h => h.DadosNovosJson);

        builder.HasIndex(h => h.PlantaId);
    }
}
