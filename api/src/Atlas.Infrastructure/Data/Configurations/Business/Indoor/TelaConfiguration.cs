using Atlas.Domain.Business.Indoor.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Business.Indoor;

public class TelaConfiguration : IEntityTypeConfiguration<Tela>
{
    public void Configure(EntityTypeBuilder<Tela> builder)
    {
        builder.ToTable("tela", "indoor", table =>
        {
            table.HasCheckConstraint(
                "ck_tela_codigo_pareamento",
                $"codigo_pareamento IS NULL OR (codigo_pareamento ~ '^[A-Z0-9]{{{Tela.TamanhoCodigoPareamento}}}$' AND codigo_pareamento_expira_em IS NOT NULL)");

            table.HasCheckConstraint(
                "ck_tela_pareada_em",
                "(dispositivo_id IS NULL) = (pareada_em IS NULL)");
        });

        builder.Property(t => t.Nome).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Localizacao).HasMaxLength(150);
        builder.Property(t => t.IdentificacaoAparelho).HasMaxLength(100);
        // varchar e não char(8): ver MidiaConfiguration (histórico da plataforma).
        builder.Property(t => t.CodigoPareamento).HasMaxLength(Tela.TamanhoCodigoPareamento);
        builder.Property(t => t.DispositivoId).HasMaxLength(100);
        builder.Property(t => t.VersaoCredencial).HasDefaultValue(1);

        builder.Ignore(t => t.Pareada);

        builder.HasIndex(t => new { t.ParceiroId, t.Nome })
            .IsUnique();

        // A TV digita só o código: ele precisa achar uma tela sem ambiguidade.
        builder.HasIndex(t => t.CodigoPareamento)
            .IsUnique()
            .HasFilter("codigo_pareamento IS NOT NULL");

        builder.HasOne(t => t.Parceiro)
            .WithMany()
            .HasForeignKey(t => t.ParceiroId)
            .OnDelete(DeleteBehavior.Restrict);

        // Playlist em uso não pode ser apagada: a tela ficaria sem grade sem ninguém ver.
        builder.HasOne(t => t.Playlist)
            .WithMany()
            .HasForeignKey(t => t.PlaylistId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Estado)
            .WithOne()
            .HasForeignKey<EstadoTela>(e => e.TelaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class EstadoTelaConfiguration : IEntityTypeConfiguration<EstadoTela>
{
    public void Configure(EntityTypeBuilder<EstadoTela> builder)
    {
        builder.ToTable("estado_tela", "indoor");

        builder.HasKey(e => e.TelaId);

        builder.Property(e => e.VersaoApp).HasMaxLength(30);
        builder.Property(e => e.VersaoGrade).HasMaxLength(64);

        // Varredura das TVs que pararam de sincronizar.
        builder.HasIndex(e => e.UltimaSincronizacao);
    }
}
