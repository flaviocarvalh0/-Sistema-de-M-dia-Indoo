namespace Atlas.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<long>
{
    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool MustChangePassword { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public long? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public bool IsSuperUser { get; set; }
}
