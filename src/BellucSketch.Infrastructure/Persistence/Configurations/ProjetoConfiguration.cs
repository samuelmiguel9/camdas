using BellucSketch.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BellucSketch.Infrastructure.Persistence.Configurations;

public sealed class ProjetoConfiguration : IEntityTypeConfiguration<Projeto>
{
    public void Configure(EntityTypeBuilder<Projeto> builder)
    {
        builder.ToTable("Projetos");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Nome).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Descricao).HasMaxLength(2000);
        builder.Property(p => p.CriadoPorId).IsRequired();
        builder.Property(p => p.DataCriacao).IsRequired();
        builder.Property(p => p.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
    }
}
