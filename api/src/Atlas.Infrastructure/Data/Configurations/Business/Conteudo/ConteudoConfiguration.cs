using Atlas.Domain.Business.Conteudo.Entities;
using Atlas.Domain.Business.Parceiros.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Business.Conteudo;

public class FonteNoticiaConfiguration : IEntityTypeConfiguration<FonteNoticia>
{
    public void Configure(EntityTypeBuilder<FonteNoticia> builder)
    {
        builder.ToTable("fonte_noticia", "indoor", table =>
            table.HasCheckConstraint("ck_fonte_noticia_url", "url_feed ~ '^https?://'"));

        builder.Property(f => f.Nome).HasMaxLength(100).IsRequired();
        builder.Property(f => f.UrlFeed).HasMaxLength(500).IsRequired();
        builder.Property(f => f.Credito).HasMaxLength(100).IsRequired();
        builder.Property(f => f.UltimoErro).HasMaxLength(500);

        builder.HasIndex(f => f.Nome).IsUnique();
        builder.HasIndex(f => f.UrlFeed).IsUnique();
    }
}

public class NoticiaConfiguration : IEntityTypeConfiguration<Noticia>
{
    public void Configure(EntityTypeBuilder<Noticia> builder)
    {
        builder.ToTable("noticia", "indoor", table =>
        {
            table.HasCheckConstraint("ck_noticia_hash_imagem", "hash_imagem IS NULL OR hash_imagem ~ '^[0-9a-f]{64}$'");

            // A imagem vem completa (chave, tipo, tamanho e hash) ou não vem.
            table.HasCheckConstraint(
                "ck_noticia_imagem_completa",
                "(chave_imagem IS NULL) = (hash_imagem IS NULL) AND (chave_imagem IS NULL) = (tamanho_imagem_bytes IS NULL) AND (chave_imagem IS NULL) = (tipo_conteudo_imagem IS NULL)");
        });

        builder.Property(n => n.Identificador).HasMaxLength(500).IsRequired();
        builder.Property(n => n.Titulo).HasMaxLength(300).IsRequired();
        builder.Property(n => n.ChaveImagem).HasMaxLength(300);
        builder.Property(n => n.TipoConteudoImagem).HasMaxLength(50);
        // varchar e não char(64), como em midia.hash.
        builder.Property(n => n.HashImagem).HasMaxLength(64);

        builder.HasIndex(n => new { n.FonteNoticiaId, n.Identificador }).IsUnique();
        builder.HasIndex(n => n.PublicadaEm);

        // Apagar a fonte apaga as notícias dela (os arquivos saem pelo comando).
        builder.HasOne(n => n.Fonte)
            .WithMany()
            .HasForeignKey(n => n.FonteNoticiaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PrevisaoClimaConfiguration : IEntityTypeConfiguration<PrevisaoClima>
{
    public void Configure(EntityTypeBuilder<PrevisaoClima> builder)
    {
        builder.ToTable("previsao_clima", "indoor");

        builder.Property(p => p.CidadeConsultada).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Local).HasMaxLength(200);
        builder.Property(p => p.TemperaturaC).HasPrecision(4, 1);
        builder.Property(p => p.MinimaC).HasPrecision(4, 1);
        builder.Property(p => p.MaximaC).HasPrecision(4, 1);
        builder.Property(p => p.UltimoErro).HasMaxLength(500);
        builder.Property(p => p.PrevisaoDias).HasMaxLength(2000);

        builder.HasIndex(p => p.ParceiroId).IsUnique();

        builder.HasOne<Parceiro>()
            .WithMany()
            .HasForeignKey(p => p.ParceiroId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CotacaoConfiguration : IEntityTypeConfiguration<Cotacao>
{
    public void Configure(EntityTypeBuilder<Cotacao> builder)
    {
        builder.ToTable("cotacao", "indoor");

        builder.Property(c => c.Moeda).HasMaxLength(3).IsRequired();
        builder.Property(c => c.Compra).HasPrecision(12, 4);
        builder.Property(c => c.Venda).HasPrecision(12, 4);
        builder.Property(c => c.FechamentoAnterior).HasPrecision(12, 4);
        builder.Property(c => c.Boletim).HasMaxLength(50);
        builder.Property(c => c.UltimoErro).HasMaxLength(500);
        builder.Ignore(c => c.VariacaoPercentual);

        builder.HasIndex(c => c.Moeda).IsUnique();
    }
}
