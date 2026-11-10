namespace HvacErp.Application.Common;

/// <summary>
/// Which tenant and stores the current unit of work may see and write. The database context reads
/// it on every query (global filters) and every save (stamping and cross-tenant write guard), so a
/// service can neither forget the filter nor reach another tenant's rows.
/// <para>
/// Three shapes exist: a request by a member of a tenant (<see cref="TenantId"/> set), a request by
/// someone with no membership (<see cref="TenantId"/> null — sees no tenant-owned row), and trusted
/// in-process code with no request behind it — workers, seeding (<see cref="IsSystem"/>).
/// </para>
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Trusted cross-tenant context: filters are off and nothing is stamped, so every row written
    /// must already carry its tenant (and store). Never true for an HTTP request.
    /// </summary>
    bool IsSystem { get; }

    /// <summary>The tenant being worked in, or null when the caller belongs to none.</summary>
    int? TenantId { get; }

    /// <summary>The stores whose rows the caller reads: the selected store, else every store they may work in.</summary>
    IReadOnlyCollection<int> StoreIds { get; }

    /// <summary>
    /// The store new quotes and repairs are created in: the selected store, else the caller's only
    /// store. Null when they work in several and selected none.
    /// </summary>
    int? StoreId { get; }
}

/// <summary>A fixed <see cref="ITenantContext"/>: one tenant and its stores, or the system context.</summary>
public sealed class FixedTenantContext : ITenantContext
{
    private FixedTenantContext(bool isSystem, int? tenantId, IReadOnlyCollection<int> storeIds, int? storeId)
    {
        IsSystem = isSystem;
        TenantId = tenantId;
        StoreIds = storeIds;
        StoreId = storeId;
    }

    /// <summary>Trusted cross-tenant context (workers, migrations, seeding across tenants).</summary>
    public static FixedTenantContext System { get; } = new(true, null, [], null);

    /// <summary>No tenant: every tenant-owned query is empty and every tenant-owned insert is refused.</summary>
    public static FixedTenantContext None { get; } = new(false, null, [], null);

    /// <summary>Works in <paramref name="tenantId"/> across <paramref name="storeIds"/>, writing to the first unless <paramref name="activeStoreId"/> says otherwise.</summary>
    public static FixedTenantContext For(int tenantId, IReadOnlyCollection<int> storeIds, int? activeStoreId = null) =>
        new(false, tenantId, storeIds, activeStoreId ?? (storeIds.Count == 1 ? storeIds.First() : null));

    /// <summary>Works in one store of <paramref name="tenantId"/>.</summary>
    public static FixedTenantContext For(int tenantId, int storeId) => For(tenantId, [storeId], storeId);

    /// <inheritdoc />
    public bool IsSystem { get; }

    /// <inheritdoc />
    public int? TenantId { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<int> StoreIds { get; }

    /// <inheritdoc />
    public int? StoreId { get; }
}

/// <summary>Guards over <see cref="ITenantContext"/> for services that need a definite tenant or store.</summary>
public static class TenantContextExtensions
{
    /// <summary>The current tenant.</summary>
    /// <exception cref="ForbiddenException">The caller belongs to no tenant.</exception>
    public static int RequireTenantId(this ITenantContext tenant) =>
        tenant.TenantId ?? throw new ForbiddenException("You do not have access to a company yet.");

    /// <summary>The store a new store-level record goes to.</summary>
    /// <exception cref="BusinessRuleException">The caller works in several stores and selected none.</exception>
    public static int RequireStoreId(this ITenantContext tenant) =>
        tenant.StoreId ?? throw new BusinessRuleException("Select the store you are working in first.");

    /// <summary>Whether the caller may read and write rows of <paramref name="storeId"/>.</summary>
    public static bool CanAccessStore(this ITenantContext tenant, int storeId) =>
        tenant.IsSystem || tenant.StoreIds.Contains(storeId);
}
