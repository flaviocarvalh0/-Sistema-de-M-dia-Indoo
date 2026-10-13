using Atlas.Domain.Platform.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Platform;

public class EmailMessageAttachmentConfiguration
    : IEntityTypeConfiguration<EmailMessageAttachment>
{
    public void Configure(
        EntityTypeBuilder<EmailMessageAttachment> builder)
    {
        builder.ToTable(
            "email_message_attachment",
            "platform");

        builder.Property(attachment =>
                attachment.EmailMessageId)
            .IsRequired();

        builder.Property(attachment =>
                attachment.FileContentId)
            .IsRequired();

        builder.Property(attachment =>
                attachment.Disposition)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(attachment =>
                attachment.FileName)
            .HasMaxLength(255);

        builder.Property(attachment =>
                attachment.ContentId)
            .HasMaxLength(255);

        builder.HasOne(attachment =>
                attachment.FileContent)
            .WithMany()
            .HasForeignKey(attachment =>
                attachment.FileContentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(attachment => new
        {
            attachment.EmailMessageId,
            attachment.FileContentId
        })
            .HasDatabaseName(
                "ix_email_message_attachment_message_file");

        builder.HasIndex(attachment => new
        {
            attachment.EmailMessageId,
            attachment.ContentId
        })
            .IsUnique()
            .HasFilter("\"content_id\" IS NOT NULL")
            .HasDatabaseName(
                "ux_email_message_attachment_message_content_id");
    }
}
