using Atlas.Domain.Platform.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Platform;

public class EntityAttachmentConfiguration
    : IEntityTypeConfiguration<EntityAttachment>
{
    public void Configure(
        EntityTypeBuilder<EntityAttachment> builder)
    {
        builder.ToTable(
            "entity_attachment",
            "platform");

        builder.Property(attachment =>
                attachment.EntityMetadataId)
            .IsRequired();

        builder.Property(attachment =>
                attachment.RecordId)
            .HasColumnName("entity_record_id")
            .IsRequired();

        builder.Property(attachment =>
                attachment.FileContentId)
            .IsRequired();

        builder.Property(attachment =>
                attachment.Title)
            .HasMaxLength(250);

        builder.Property(attachment =>
                attachment.Description)
            .HasMaxLength(1000);

        builder.Property(attachment =>
                attachment.ReferenceDate);

        builder.HasOne(attachment =>
                attachment.EntityMetadata)
            .WithMany()
            .HasForeignKey(attachment =>
                attachment.EntityMetadataId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(attachment =>
                attachment.FileContent)
            .WithMany()
            .HasForeignKey(attachment =>
                attachment.FileContentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(attachment => new
        {
            attachment.EntityMetadataId,
            attachment.RecordId
        })
            .HasDatabaseName(
                "ix_entity_attachment_entity_metadata_id_entity_record_id");

        builder.HasIndex(attachment =>
                attachment.FileContentId)
            .HasDatabaseName(
                "ix_entity_attachment_file_content_id");
    }
}
