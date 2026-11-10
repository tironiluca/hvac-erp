using HvacErp.Domain.Enums;

namespace HvacErp.Domain.Entities;

/// <summary>
/// A row that belongs to one <see cref="Tenant"/>. The database context filters every query on it
/// and stamps it on insert, so application code never sets or compares it by hand.
/// </summary>
public interface ITenantOwned
{
    /// <summary>The owning <see cref="Tenant"/>.</summary>
    int TenantId { get; set; }
}

/// <summary>
/// A row that lives in one <see cref="Store"/> of its tenant (quotes, repairs, installations, F-Gas
/// records, ERP exports). Readable only by users who may work in that store.
/// </summary>
public interface IStoreOwned : ITenantOwned
{
    /// <summary>The owning <see cref="Store"/>.</summary>
    int StoreId { get; set; }
}

/// <summary>
/// A company using the product. Everything a company owns — catalogue, clients, quotes, repairs,
/// templates — carries its id and is invisible to every other tenant.
/// </summary>
public class Tenant
{
    /// <summary>The tenant created by the migration for a deployment's existing data.</summary>
    public const int DefaultId = 1;

    public int Id { get; set; }

    /// <summary>Display name (shown in the tenant switcher).</summary>
    public string Name { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string? VatNumber { get; set; }

    /// <summary>False suspends the tenant: its members lose access, its data stays.</summary>
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Store> Stores { get; set; } = new List<Store>();
    public ICollection<TenantMembership> Memberships { get; set; } = new List<TenantMembership>();
}

/// <summary>A branch / shop of a <see cref="Tenant"/>. Quotes are numbered per store.</summary>
public class Store : ITenantOwned
{
    /// <summary>The store created by the migration for a deployment's existing data.</summary>
    public const int DefaultId = 1;

    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    /// <summary>False hides the store from the switcher; its history stays readable.</summary>
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// The grant that lets a user work in a tenant, with the role they hold there. A user without a
/// membership has no access to anything; a user may belong to several tenants.
/// </summary>
public class TenantMembership : ITenantOwned
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public UserRole Role { get; set; } = UserRole.Sales;

    /// <summary>True: every store of the tenant, present and future. False: only those in <see cref="Stores"/>.</summary>
    public bool AllStores { get; set; }

    /// <summary>The tenant's installer record this user corresponds to, if any.</summary>
    public int? InstallerId { get; set; }
    public Installer? Installer { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<StoreAccess> Stores { get; set; } = new List<StoreAccess>();
}

/// <summary>One store a member without <see cref="TenantMembership.AllStores"/> may work in.</summary>
public class StoreAccess : ITenantOwned
{
    public int TenantMembershipId { get; set; }
    public TenantMembership Membership { get; set; } = null!;

    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;

    public int TenantId { get; set; }
}
