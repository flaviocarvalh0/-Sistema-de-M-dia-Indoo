using Atlas.Domain.Business.Indoor.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Business.Indoor;

/// <summary>Prova de veiculação (ADR-023): sem FKs, para o histórico sobreviver às exclusões.</summary>
public class RegistroExibicaoConfiguration : IEntityTypeConfiguration<RegistroExibicao>
{
    public void Configure(EntityTypeBuilder<RegistroExibicao> builder)
    {
        builder.ToTable("registro_exibicao", "indoor", table =>
        {
            table.HasCheckConstraint("ck_registro_exibicao_duracao", "duracao_ms BETWEEN 0 AND 7200000");
        });

        // Reenvio do mesmo lote não duplica.
        builder.HasIndex(r => new { r.TelaId, r.IdLocal }).IsUnique();
        builder.HasIndex(r => new { r.ClienteId, r.Inicio });
        builder.HasIndex(r => new { r.TelaId, r.Inicio });
    }
}

public class LeituraQrConfiguration : IEntityTypeConfiguration<LeituraQr>
{
    public void Configure(EntityTypeBuilder<LeituraQr> builder)
    {
        builder.ToTable("leitura_qr", "indoor", table =>
        {
            table.HasCheckConstraint("ck_leitura_qr_visitante", $"visitante ~ '^[0-9a-f]{{{LeituraQr.TamanhoVisitante}}}$'");
        });

        builder.Property(l => l.Visitante).HasMaxLength(LeituraQr.TamanhoVisitante).IsRequired();
        builder.HasIndex(l => new { l.ClienteId, l.Momento });
        builder.HasIndex(l => new { l.MidiaId, l.Momento });
    }
}
