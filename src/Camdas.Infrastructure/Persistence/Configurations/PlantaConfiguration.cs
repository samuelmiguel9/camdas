using Camdas.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Camdas.Infrastructure.Persistence.Configurations;

/// <summary>
/// Planta é o agregado raiz: Camadas só são alcançadas por navegação a partir daqui. A coleção usa
/// o campo privado (_camadas) — não a propriedade pública (que expõe apenas um wrapper
/// somente-leitura) — via <see cref="PropertyAccessMode.Field"/>.
/// </summary>
public sealed class PlantaConfiguration : IEntityTypeConfiguration<Planta>
{
    public void Configure(EntityTypeBuilder<Planta> builder)
    {
        builder.ToTable("Plantas");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.ProjetoId).IsRequired();
        builder.Property(p => p.Nome).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Descricao).HasMaxLength(2000);
        builder.Property(p => p.NomeCliente).HasMaxLength(200);
        builder.Property(p => p.TipoArquivoOrigem).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.CaminhoArquivoOriginal).IsRequired().HasMaxLength(500);
        builder.Property(p => p.DataImportacao).IsRequired();

        builder.HasMany(p => p.Camadas)
            .WithOne()
            .HasForeignKey(c => c.PlantaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Camadas).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
