using Atlas.Domain.Business.Clientes.Entities;
using Atlas.Domain.Business.Clientes.Enums;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Business.Clientes;

public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("cliente", "business", table =>
        {
            // Regras que o banco garante mesmo se um handler esquecer.
            table.HasCheckConstraint(
                "ck_cliente_motivo_bloqueio",
                $"situacao <> {(int)SituacaoCliente.Bloqueado} OR motivo_bloqueio IS NOT NULL");

            table.HasCheckConstraint(
                "ck_cliente_documento",
                "documento IS NULL OR documento ~ '^[0-9]{11}$' OR documento ~ '^[0-9A-Z]{12}[0-9]{2}$'");

            table.HasCheckConstraint("ck_cliente_telefone", "telefone IS NULL OR telefone ~ '^[0-9]{10,11}$'");

            table.HasCheckConstraint("ck_cliente_cota", "cota_armazenamento_mb BETWEEN 1 AND 1048576");

            table.HasCheckConstraint("ck_cliente_limite_midias_ativas", "limite_midias_ativas BETWEEN 1 AND 1000");

            // Diretório das TVs: o aceite tem data; os contatos já vêm normalizados.
            table.HasCheckConstraint(
                "ck_cliente_diretorio_aceite",
                "(exibir_no_diretorio_publico) = (diretorio_aceito_em IS NOT NULL)");
            table.HasCheckConstraint("ck_cliente_whatsapp_publico", "whatsapp_publico IS NULL OR whatsapp_publico ~ '^[0-9]{12,15}$'");
            table.HasCheckConstraint("ck_cliente_instagram_publico", "instagram_publico IS NULL OR instagram_publico ~ '^[A-Za-z0-9._]{1,30}$'");
            table.HasCheckConstraint("ck_cliente_site_publico", "site_publico IS NULL OR site_publico ~ '^https?://'");
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
        builder.Property(p => p.Categoria).HasMaxLength(Cliente.TamanhoMaximoCategoria);
        builder.Property(p => p.DescricaoPublica).HasMaxLength(Cliente.TamanhoMaximoDescricaoPublica);
        builder.Property(p => p.WhatsappPublico).HasMaxLength(15);
        builder.Property(p => p.InstagramPublico).HasMaxLength(30);
        builder.Property(p => p.SitePublico).HasMaxLength(300);

        builder.Property(p => p.FusoHorario)
            .HasMaxLength(64)
            .HasDefaultValue("America/Sao_Paulo")
            .IsRequired();

        builder.Property(p => p.CotaArmazenamentoMb)
            .HasDefaultValue(Cliente.CotaPadraoMb);

        // Clientes que já existiam entram com o plano padrão (1 ativa); quem
        // já tinha mais ativas continua assim (direito adquirido).
        builder.Property(p => p.LimiteMidiasAtivas)
            .HasDefaultValue(Cliente.LimiteMidiasAtivasPadrao);

        builder.Ignore(p => p.Bloqueado);
        builder.Ignore(p => p.CotaArmazenamentoBytes);
    }
}
