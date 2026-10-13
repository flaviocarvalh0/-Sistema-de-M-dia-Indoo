using Atlas.Domain.Platform.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Platform;

public class SystemParameterConfiguration : IEntityTypeConfiguration<SystemParameter>
{
    public void Configure(EntityTypeBuilder<SystemParameter> builder)
    {
        builder.ToTable("system_parameter", "platform");

        builder.HasIndex(p => p.Key)
            .IsUnique();

        builder.Property(p => p.Key)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Value)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.Group)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.DataType)
            .HasMaxLength(50)
            .IsRequired();
    }
}
