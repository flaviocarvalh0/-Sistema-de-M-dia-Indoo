using Atlas.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Platform;

public class ApplicationUserClaimConfiguration : IEntityTypeConfiguration<ApplicationUserClaim>
{
    public void Configure(EntityTypeBuilder<ApplicationUserClaim> builder)
    {
        builder.Property(e => e.ModuleId)
            .HasColumnName("module_id");

        builder.Property(e => e.EntityName)
            .HasColumnName("entity_name")
            .HasMaxLength(100);
    }
}
