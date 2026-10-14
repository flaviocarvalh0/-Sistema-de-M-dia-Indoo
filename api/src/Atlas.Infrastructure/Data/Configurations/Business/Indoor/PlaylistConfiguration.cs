using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Business.Indoor;

public class PlaylistConfiguration : IEntityTypeConfiguration<Playlist>
{
    public void Configure(EntityTypeBuilder<Playlist> builder)
    {
        builder.ToTable("playlist", "indoor");

        builder.Property(p => p.Nome).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Descricao).HasMaxLength(300);

        // A playlist é da rede (da operadora), não de um parceiro.
        builder.HasIndex(p => p.Nome)
            .IsUnique();

        // Os itens são parte da playlist: removidos da lista, somem do banco.
        builder.HasMany(p => p.Itens)
            .WithOne()
            .HasForeignKey(i => i.PlaylistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Itens)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class ItemPlaylistConfiguration : IEntityTypeConfiguration<ItemPlaylist>
{
    public void Configure(EntityTypeBuilder<ItemPlaylist> builder)
    {
        builder.ToTable("item_playlist", "indoor", table =>
        {
            table.HasCheckConstraint("ck_item_playlist_ordem", "ordem >= 0");

            table.HasCheckConstraint(
                "ck_item_playlist_dias_semana",
                $"dias_semana BETWEEN 1 AND {(int)DiasSemana.Todos}");
        });

        // Sem índice único em (playlist, ordem): DefinirItens renumera tudo
        // numa gravação só, e o PostgreSQL checaria a unicidade linha a linha.
        builder.HasIndex(i => new { i.PlaylistId, i.Ordem });

        // Mídia em uso numa playlist não pode ser apagada; inativar tira da grade.
        builder.HasOne(i => i.Midia)
            .WithMany()
            .HasForeignKey(i => i.MidiaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
