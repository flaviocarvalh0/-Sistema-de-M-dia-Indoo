using Atlas.Domain.Platform.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Platform;

public class EmailMessageRecipientConfiguration
    : IEntityTypeConfiguration<EmailMessageRecipient>
{
    public void Configure(
        EntityTypeBuilder<EmailMessageRecipient> builder)
    {
        builder.ToTable(
            "email_message_recipient",
            "platform");

        builder.Property(recipient =>
                recipient.EmailMessageId)
            .IsRequired();

        builder.Property(recipient =>
                recipient.Name)
            .HasMaxLength(200);

        builder.Property(recipient =>
                recipient.Email)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(recipient =>
                recipient.NormalizedEmail)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(recipient =>
                recipient.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(recipient => new
        {
            recipient.EmailMessageId,
            recipient.NormalizedEmail
        })
            .IsUnique()
            .HasDatabaseName(
                "ux_email_message_recipient_message_email");

        builder.HasIndex(recipient =>
                recipient.NormalizedEmail)
            .HasDatabaseName(
                "ix_email_message_recipient_normalized_email");

        builder.HasIndex(recipient => new
        {
            recipient.EmailMessageId,
            recipient.Type
        })
            .HasDatabaseName(
                "ix_email_message_recipient_message_type");
    }
}
