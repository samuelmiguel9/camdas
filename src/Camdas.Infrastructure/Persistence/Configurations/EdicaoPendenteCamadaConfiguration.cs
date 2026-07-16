using Camdas.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Camdas.Infrastructure.Persistence.Configurations;

public sealed class EdicaoPendenteCamadaConfiguration : IEntityTypeConfiguration<EdicaoPendenteCamada>
{
    public void Configure(EntityTypeBuilder<EdicaoPendenteCamada> builder)
    {
        builder.ToTable("EdicoesPendentesCamada");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PlantaId).IsRequired();
        builder.Property(e => e.CamadaId);
        builder.Property(e => e.TipoOperacao).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.DadosAntesJson);
        builder.Property(e => e.DadosDepoisJson).IsRequired();
        builder.Property(e => e.Responsavel).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Motivo).IsRequired().HasMaxLength(500);
        builder.Property(e => e.DataSolicitacao).IsRequired();
        builder.Property(e => e.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.DataResposta);
        builder.Property(e => e.MotivoRejeicao).HasMaxLength(500);

        builder.HasIndex(e => e.PlantaId);
        builder.HasIndex(e => e.Status);
    }
}
