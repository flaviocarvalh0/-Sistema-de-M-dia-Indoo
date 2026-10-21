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

/// <summary>Diretório das TVs (ADR-027): sem FKs, como as leituras do QR.</summary>
public class InteracaoDiretorioConfiguration : IEntityTypeConfiguration<InteracaoDiretorio>
{
    public void Configure(EntityTypeBuilder<InteracaoDiretorio> builder)
    {
        builder.ToTable("interacao_diretorio", "indoor", table =>
        {
            table.HasCheckConstraint("ck_interacao_diretorio_visitante", $"visitante ~ '^[0-9a-f]{{{LeituraQr.TamanhoVisitante}}}$'");
            table.HasCheckConstraint("ck_interacao_diretorio_evento", "evento IN (1, 2, 3)");
            // Contato tocado tem dono e tipo; acesso e "anuncie" não têm.
            table.HasCheckConstraint("ck_interacao_diretorio_contato",
                "(evento = 2 AND cliente_id IS NOT NULL AND contato IS NOT NULL) OR (evento <> 2 AND cliente_id IS NULL AND contato IS NULL)");
        });

        builder.Property(i => i.Visitante).HasMaxLength(LeituraQr.TamanhoVisitante).IsRequired();
        builder.HasIndex(i => new { i.TelaId, i.Momento });
        builder.HasIndex(i => new { i.ClienteId, i.Momento });
    }
}

public class ReservaEnvioConfiguration : IEntityTypeConfiguration<ReservaEnvio>
{
    public void Configure(EntityTypeBuilder<ReservaEnvio> builder)
    {
        builder.ToTable("reserva_envio", "indoor", t => t.HasCheckConstraint("ck_reserva_envio_tamanho", "tamanho_bytes > 0"));
        builder.Property(r => r.Chave).HasMaxLength(300).IsRequired();
        builder.HasIndex(r => r.Chave).IsUnique();
        builder.HasIndex(r => r.ExpiraEm);
    }
}

public class InventarioArmazenamentoConfiguration : IEntityTypeConfiguration<InventarioArmazenamento>
{
    public void Configure(EntityTypeBuilder<InventarioArmazenamento> builder)
    {
        builder.ToTable("inventario_armazenamento", "indoor", t => t.HasCheckConstraint("ck_inventario_armazenamento_unico", "id = 1"));
        builder.Property(i => i.Id).ValueGeneratedNever();
    }
}
