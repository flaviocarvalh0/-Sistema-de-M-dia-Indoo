using Atlas.Domain.Business.Parceiros.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Business.Parceiros;

public class ParceiroConfiguration : IEntityTypeConfiguration<Parceiro>
{
    public void Configure(EntityTypeBuilder<Parceiro> builder)
    {
        builder.ToTable("parceiro", "business", table =>
        {
            table.HasCheckConstraint(
                "ck_parceiro_documento",
                "documento IS NULL OR documento ~ '^[0-9]{11}$' OR documento ~ '^[0-9A-Z]{12}[0-9]{2}$'");
            table.HasCheckConstraint("ck_parceiro_telefone", "telefone IS NULL OR telefone ~ '^[0-9]{10,11}$'");
        });

        builder.Property(p => p.Nome).HasMaxLength(150).IsRequired();
        builder.Property(p => p.NomeResponsavel).HasMaxLength(150);
        builder.Property(p => p.Telefone).HasMaxLength(11);
        builder.Property(p => p.Email).HasMaxLength(254);
        builder.Property(p => p.Documento).HasMaxLength(14);
        builder.Property(p => p.Endereco).HasMaxLength(250);
        builder.Property(p => p.Cidade).HasMaxLength(100);
        builder.Property(p => p.Observacao).HasColumnType("text");

        builder.Property(p => p.FusoHorario)
            .HasMaxLength(64)
            .HasDefaultValue("America/Sao_Paulo")
            .IsRequired();

        builder.HasIndex(p => p.Nome);

        // O mesmo cliente pode ser dono de vários locais (unidades).
        builder.HasIndex(p => p.ClienteId);
        builder.HasOne(p => p.Cliente)
            .WithMany()
            .HasForeignKey(p => p.ClienteId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
