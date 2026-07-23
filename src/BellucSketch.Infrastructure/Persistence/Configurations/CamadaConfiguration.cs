using BellucSketch.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellucSketch.Infrastructure.Persistence.Configurations;

public sealed class CamadaConfiguration : IEntityTypeConfiguration<Camada>
{
    public void Configure(EntityTypeBuilder<Camada> builder)
    {
        builder.ToTable("Camadas");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.PlantaId).IsRequired();
        builder.Property(c => c.Nome).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Visivel).IsRequired();
        builder.Property(c => c.Bloqueada).IsRequired();
        builder.Property(c => c.BloqueioAlpha).IsRequired().HasDefaultValue(false);
        builder.Property(c => c.Ordem).IsRequired();
        builder.Property(c => c.Opacidade).IsRequired().HasDefaultValue(1.0);
        builder.Property(c => c.ImagemRasterCaminho).HasMaxLength(500);
    }
}
