using Atlas.Domain.Business.Indoor.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Business.Indoor;

public class MidiaConfiguration : IEntityTypeConfiguration<Midia>
{
    public void Configure(EntityTypeBuilder<Midia> builder)
    {
        builder.ToTable("midia", "indoor", table =>
        {
            table.HasCheckConstraint("ck_midia_hash", "hash ~ '^[0-9a-f]{64}$'");

            table.HasCheckConstraint(
                "ck_midia_tamanho",
                $"tamanho_bytes BETWEEN 1 AND {Midia.TamanhoMaximoBytes}");

            table.HasCheckConstraint(
                "ck_midia_duracao",
                $"duracao_segundos BETWEEN 1 AND {Midia.DuracaoMaximaSegundos}");

            table.HasCheckConstraint(
                "ck_midia_periodo",
                "inicio_exibicao IS NULL OR fim_exibicao IS NULL OR fim_exibicao >= inicio_exibicao");
        });

        builder.Property(m => m.Titulo).HasMaxLength(150).IsRequired();
        builder.Property(m => m.ChaveArmazenamento).HasMaxLength(300).IsRequired();
        builder.Property(m => m.TipoConteudo).HasMaxLength(50).IsRequired();
        // varchar e não char(64): o inspetor do histórico da plataforma não
        // reconhece o tamanho de char(n) e recusaria a tabela histórica na
        // subida seguinte. O tamanho exato fica na ck_midia_hash.
        builder.Property(m => m.Hash).HasMaxLength(64).IsRequired();

        builder.Property(m => m.DuracaoSegundos)
            .HasDefaultValue(Midia.DuracaoPadraoSegundos);

        builder.HasIndex(m => m.ChaveArmazenamento)
            .IsUnique();

        // Listagem do painel e soma da cota por parceiro.
        builder.HasIndex(m => new { m.ParceiroId, m.Ativo });

        builder.HasOne(m => m.Parceiro)
            .WithMany()
            .HasForeignKey(m => m.ParceiroId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
