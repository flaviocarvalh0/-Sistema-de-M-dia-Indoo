using Atlas.Domain.Platform.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Platform;

public class SystemModuleConfiguration : IEntityTypeConfiguration<SystemModule>
{
    public void Configure(EntityTypeBuilder<SystemModule> builder)
    {
        builder.ToTable("system_module", "platform");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .HasMaxLength(250);

        builder.Property(x => x.RoutePrefix)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.Property(x => x.HexColor)
            .HasMaxLength(7);

        builder.HasOne(x => x.SystemIcon)
            .WithMany()
            .HasForeignKey(x => x.SystemIconId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
