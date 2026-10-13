using Atlas.Domain.Platform.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Platform;

public class SystemIconConfiguration : IEntityTypeConfiguration<SystemIcon>
{
    public void Configure(EntityTypeBuilder<SystemIcon> builder)
    {
        builder.ToTable("system_icon", "platform");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Extension)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.SizeInBytes)
            .IsRequired();

        builder.Property(x => x.Data)
            .IsRequired();

        builder.HasIndex(x => x.Name)
            .IsUnique();
    }
}
