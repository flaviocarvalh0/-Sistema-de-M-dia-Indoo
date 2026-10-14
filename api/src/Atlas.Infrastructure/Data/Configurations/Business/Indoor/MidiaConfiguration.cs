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

            // QR code completo (tipo, destino, posição, tamanho e conteúdo) ou nenhum.
            table.HasCheckConstraint(
                "ck_midia_qr_code_completo",
                "(qr_tipo IS NULL) = (qr_conteudo IS NULL) AND (qr_tipo IS NULL) = (qr_destino IS NULL) "
                + "AND (qr_tipo IS NULL) = (qr_posicao IS NULL) AND (qr_tipo IS NULL) = (qr_tamanho IS NULL)");

            table.HasCheckConstraint("ck_midia_qr_valor", "qr_valor IS NULL OR qr_valor > 0");
        });

        builder.Property(m => m.QrDestino).HasMaxLength(500);
        builder.Property(m => m.QrMensagem).HasMaxLength(200);
        builder.Property(m => m.QrValor).HasPrecision(12, 2);
        builder.Property(m => m.QrNomeRecebedor).HasMaxLength(25);
        builder.Property(m => m.QrCidadeRecebedor).HasMaxLength(15);
        builder.Property(m => m.QrLegenda).HasMaxLength(Midia.TamanhoMaximoLegendaQrCode);
        builder.Property(m => m.QrConteudo).HasMaxLength(Midia.TamanhoMaximoConteudoQrCode);

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
