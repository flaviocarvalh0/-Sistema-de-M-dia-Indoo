using Atlas.Domain.Platform.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Platform;

public class SystemMenuItemConfiguration : IEntityTypeConfiguration<SystemMenuItem>
{
    public void Configure(EntityTypeBuilder<SystemMenuItem> builder)
    {
        builder.ToTable("system_menu_item", "platform");

        builder.HasKey(x => x.Id);

        // =========================================================================
        // Propriedades Primitivas e Validações
        // =========================================================================
        builder.Property(x => x.DisplayLabel)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.Property(x => x.MenuType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(m => m.IconHexColor)
            .HasMaxLength(7);

        // =========================================================================
        // Relacionamentos e Chaves Estrangeiras
        // =========================================================================

        // 1. Relacionamento com o módulo proprietário.
        // A árvore de menu pertence ao módulo; removendo o módulo, seus menus também são removidos.
        builder.HasOne(x => x.SystemModule)
            .WithMany()
            .HasForeignKey(x => x.SystemModuleId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // 2. Relacionamento auto-referenciado para formação da árvore de menus.
        // Usamos Restrict para evitar deleções em cascata acidentais entre níveis da árvore.
        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // 3. Relacionamento opcional com o catálogo de ícones.
        // Caso o ícone seja removido, o item permanece sem ícone.
        builder.HasOne(x => x.SystemIcon)
            .WithMany()
            .HasForeignKey(x => x.SystemIconId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // 4. Relacionamento opcional com os metadados de entidade.
        // Usado quando o item representa uma tela dinâmica baseada em metadados.
        builder.HasOne(x => x.EntityMetadata)
            .WithMany()
            .HasForeignKey(x => x.EntityMetadataId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.SystemModuleId);

        builder.HasIndex(x => x.ParentId);

        builder.HasIndex(x => x.EntityMetadataId);

        builder.HasIndex(x => new { x.SystemModuleId, x.ParentId, x.SortOrder });
    }
}
