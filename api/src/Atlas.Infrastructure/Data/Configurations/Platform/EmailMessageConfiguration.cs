using Atlas.Domain.Platform.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Platform;

public class EmailMessageConfiguration
    : IEntityTypeConfiguration<EmailMessage>
{
    public void Configure(
        EntityTypeBuilder<EmailMessage> builder)
    {
        builder.ToTable(
            "email_message",
            "platform");

        builder.Property(message =>
                message.Subject)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(message =>
                message.HtmlBody)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(message =>
                message.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(message =>
                message.ErrorMessage)
            .HasColumnType("text");

        builder.Property(message =>
                message.RetryCount)
            .IsRequired();

        builder.Property(message =>
                message.SentAt);

        builder.Property(message =>
                message.LastAttemptAt);

        builder.Property(message =>
                message.RelatedEntityType)
            .HasMaxLength(100);

        builder.Property(message =>
                message.RelatedEntityId)
            .HasMaxLength(100);

        builder.HasIndex(message => new
        {
            message.Status,
            message.Created
        })
            .HasDatabaseName(
                "ix_email_message_status_created");

        builder.HasIndex(message => new
        {
            message.RelatedEntityType,
            message.RelatedEntityId
        })
            .HasDatabaseName(
                "ix_email_message_related_entity");

        builder.HasMany(message =>
                message.Recipients)
            .WithOne(recipient =>
                recipient.EmailMessage)
            .HasForeignKey(recipient =>
                recipient.EmailMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(message =>
                message.Attachments)
            .WithOne(attachment =>
                attachment.EmailMessage)
            .HasForeignKey(attachment =>
                attachment.EmailMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(message =>
                message.Recipients)
            .UsePropertyAccessMode(
                PropertyAccessMode.Field);

        builder.Navigation(message =>
                message.Attachments)
            .UsePropertyAccessMode(
                PropertyAccessMode.Field);
    }
}
