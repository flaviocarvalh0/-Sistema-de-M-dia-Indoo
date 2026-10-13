using Atlas.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Platform;

public class ApplicationRoleClaimConfiguration : IEntityTypeConfiguration<ApplicationRoleClaim>
{
    public void Configure(EntityTypeBuilder<ApplicationRoleClaim> builder)
    {
        builder.Property(e => e.ModuleId)
            .HasColumnName("module_id");

        builder.Property(e => e.EntityName)
            .HasColumnName("entity_name")
            .HasMaxLength(100);
    }
}
