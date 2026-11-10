namespace HvacErp.Domain.Entities;

/// <summary>
/// Application-side profile for a Supabase Auth identity. Authentication itself
/// (password, sessions, tokens) is entirely delegated to Supabase Auth; this row
/// identifies the person. What they may do is granted per tenant by a
/// <see cref="TenantMembership"/> — a profile without one has no access.
/// </summary>
public class AppUser
{
    /// <summary>Matches the Supabase Auth user id (auth.users.id).</summary>
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>False disables the person everywhere, whatever memberships they hold.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>May create and suspend tenants and edit platform-wide settings. Grants no access to a tenant's data.</summary>
    public bool IsPlatformAdmin { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TenantMembership> Memberships { get; set; } = new List<TenantMembership>();
}
