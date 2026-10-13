using Atlas.Domain.Platform.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Platform;

public class FileContentConfiguration
    : IEntityTypeConfiguration<FileContent>
{
    public void Configure(
        EntityTypeBuilder<FileContent> builder)
    {
        builder.ToTable(
            "file_content",
            "platform");

        builder.Property(file => file.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(file => file.ContentType)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(file => file.Extension)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(file => file.Size)
            .IsRequired();

        builder.Property(file => file.Hash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(file => file.Content)
            .HasColumnType("bytea")
            .IsRequired();

        builder.HasIndex(file => file.Hash)
            .HasDatabaseName(
                "ix_file_content_hash");
    }
}
