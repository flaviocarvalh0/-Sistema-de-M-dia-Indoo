using Atlas.Domain.Platform.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Platform;

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("user_session", "platform");

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.SessionId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.RefreshTokenHash)
            .HasMaxLength(512);

        builder.Property(x => x.RevokedReason)
            .HasMaxLength(300);

        builder.Property(x => x.IpAddress)
            .HasMaxLength(100);

        builder.Property(x => x.UserAgent)
            .HasMaxLength(500);

        builder.HasIndex(x => x.SessionId)
            .IsUnique();

        builder.HasIndex(x => x.UserId);

        builder.HasIndex(x => new { x.UserId, x.RevokedAt });

        builder.HasIndex(x => x.RefreshTokenHash);
    }
}
