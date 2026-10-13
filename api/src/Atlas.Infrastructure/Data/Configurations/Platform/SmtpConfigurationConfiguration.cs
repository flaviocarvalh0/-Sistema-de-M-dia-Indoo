using Atlas.Domain.Platform.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Platform;

public class SmtpConfigurationConfiguration : IEntityTypeConfiguration<SmtpConfiguration>
{
    public void Configure(EntityTypeBuilder<SmtpConfiguration> builder)
    {
        builder.ToTable("smtp_configuration", "platform");

        builder.Property(s => s.Host)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(s => s.UserName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(s => s.Password)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(s => s.SenderEmail)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(s => s.SenderName)
            .HasMaxLength(150);
    }
}
