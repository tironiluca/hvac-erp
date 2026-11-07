using HvacErp.Domain.Enums;

namespace HvacErp.Domain.Entities;

/// <summary>
/// Application-side profile for a Supabase Auth identity. Authentication itself
/// (password, sessions, tokens) is entirely delegated to Supabase Auth; this row
/// only carries the business-facing role and, optionally, a link to the
/// installer/operator record the user corresponds to.
/// </summary>
public class AppUser
{
    /// <summary>Matches the Supabase Auth user id (auth.users.id).</summary>
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Sales;

    public int? InstallerId { get; set; }
    public Installer? Installer { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
