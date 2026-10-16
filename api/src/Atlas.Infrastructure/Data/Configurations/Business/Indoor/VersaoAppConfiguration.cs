using Atlas.Domain.Business.Indoor.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Business.Indoor;

public class VersaoAppConfiguration : IEntityTypeConfiguration<VersaoApp>
{
    public void Configure(EntityTypeBuilder<VersaoApp> builder)
    {
        builder.ToTable("versao_app", "indoor", table =>
        {
            table.HasCheckConstraint("ck_versao_app_codigo", "codigo > 0");
            table.HasCheckConstraint("ck_versao_app_hash", "hash ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint("ck_versao_app_tamanho", $"tamanho_bytes BETWEEN 1 AND {VersaoApp.TamanhoMaximoBytes}");
        });

        builder.Property(v => v.Nome).HasMaxLength(30).IsRequired();
        builder.Property(v => v.Notas).HasMaxLength(500);
        builder.Property(v => v.ChaveArmazenamento).HasMaxLength(300).IsRequired();
        builder.Property(v => v.Hash).HasMaxLength(64).IsRequired();

        builder.HasIndex(v => v.Codigo).IsUnique();
        builder.HasIndex(v => v.ChaveArmazenamento).IsUnique();
    }
}
