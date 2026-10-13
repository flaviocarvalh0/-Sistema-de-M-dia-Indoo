using Atlas.Domain.Platform.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Platform;

public class EntityMetadataConfiguration : IEntityTypeConfiguration<EntityMetadata>
{
    public void Configure(EntityTypeBuilder<EntityMetadata> builder)
    {
        builder.ToTable("entity_metadata", "platform");

        builder.HasKey(x => x.Id);

        // =========================================================================
        // Propriedades Primitivas
        // =========================================================================
        builder.Property(x => x.TableName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.DisplayLabel)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.RouteKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.DefaultPageSize)
            .IsRequired();

        builder.Property(x => x.CodePrefix)
            .HasMaxLength(20);

        // =========================================================================
        // Índices e Restrições de Unicidade
        // =========================================================================
        builder.HasIndex(x => x.TableName)
            .IsUnique();

        builder.HasIndex(x => x.RouteKey)
            .IsUnique();

        // =========================================================================
        // Relacionamentos (Chaves Estrangeiras)
        // =========================================================================
        builder.HasOne(x => x.SystemIcon)
            .WithMany()
            .HasForeignKey(x => x.SystemIconId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.SystemModule)
            .WithMany(x => x.Entities)
            .HasForeignKey(x => x.SystemModuleId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
