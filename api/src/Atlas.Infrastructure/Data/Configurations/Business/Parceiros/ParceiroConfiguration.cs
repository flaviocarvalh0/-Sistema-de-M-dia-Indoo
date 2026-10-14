using Atlas.Domain.Business.Parceiros.Entities;
using Atlas.Domain.Business.Parceiros.Enums;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Business.Parceiros;

public class ParceiroConfiguration : IEntityTypeConfiguration<Parceiro>
{
    public void Configure(EntityTypeBuilder<Parceiro> builder)
    {
        builder.ToTable("parceiro", "business", table =>
        {
            // Regras que o banco garante mesmo se um handler esquecer.
            table.HasCheckConstraint(
                "ck_parceiro_motivo_bloqueio",
                $"situacao <> {(int)SituacaoParceiro.Bloqueado} OR motivo_bloqueio IS NOT NULL");

            table.HasCheckConstraint(
                "ck_parceiro_documento",
                "documento IS NULL OR documento ~ '^[0-9]{11}$' OR documento ~ '^[0-9A-Z]{12}[0-9]{2}$'");

            table.HasCheckConstraint("ck_parceiro_telefone", "telefone IS NULL OR telefone ~ '^[0-9]{10,11}$'");

            table.HasCheckConstraint("ck_parceiro_cota", "cota_armazenamento_mb BETWEEN 1 AND 1048576");

            table.HasCheckConstraint("ck_parceiro_limite_midias_ativas", "limite_midias_ativas BETWEEN 1 AND 1000");
        });

        builder.HasIndex(p => p.Situacao);

        builder.Property(p => p.Nome).HasMaxLength(150).IsRequired();
        builder.Property(p => p.NomeResponsavel).HasMaxLength(150);
        builder.Property(p => p.Email).HasMaxLength(254).IsRequired();
        builder.Property(p => p.Telefone).HasMaxLength(11);
        builder.Property(p => p.Documento).HasMaxLength(14);
        builder.Property(p => p.Cidade).HasMaxLength(100);
        builder.Property(p => p.MotivoBloqueio).HasMaxLength(500);
        builder.Property(p => p.ObservacaoInterna).HasColumnType("text");

        builder.Property(p => p.FusoHorario)
            .HasMaxLength(64)
            .HasDefaultValue("America/Sao_Paulo")
            .IsRequired();

        builder.Property(p => p.CotaArmazenamentoMb)
            .HasDefaultValue(Parceiro.CotaPadraoMb);

        // Parceiros que já existiam entram com o plano padrão (1 ativa); quem
        // já tinha mais ativas continua assim (direito adquirido).
        builder.Property(p => p.LimiteMidiasAtivas)
            .HasDefaultValue(Parceiro.LimiteMidiasAtivasPadrao);

        builder.Ignore(p => p.Bloqueado);
        builder.Ignore(p => p.CotaArmazenamentoBytes);
    }
}
