namespace HvacErp.Application.Common;

/// <summary>
/// Claims the sign-in resolution adds to the Supabase principal, and the request inputs it reads to
/// pick the tenant and store. The role itself is the standard role claim.
/// </summary>
public static class AppClaimTypes
{
    /// <summary>The caller's <c>AppUser</c> id; its presence marks a principal as already resolved.</summary>
    public const string UserId = "app_user_id";

    /// <summary>The tenant the request works in. Absent: the caller belongs to no tenant.</summary>
    public const string TenantId = "tenant_id";

    /// <summary>One per store the request may read.</summary>
    public const string StoreId = "store_id";

    /// <summary>The store new quotes and repairs go to, when there is exactly one candidate.</summary>
    public const string ActiveStoreId = "active_store_id";

    /// <summary>Present (<c>true</c>) for a platform administrator.</summary>
    public const string PlatformAdmin = "platform_admin";

    /// <summary>Request header selecting the tenant, for a user who belongs to several.</summary>
    public const string TenantHeader = "X-Tenant-Id";

    /// <summary>Request header narrowing the request to one store.</summary>
    public const string StoreHeader = "X-Store-Id";

    /// <summary>Query-string equivalent of <see cref="TenantHeader"/> for SignalR, whose browser client cannot set headers.</summary>
    public const string TenantQuery = "tenant";

    /// <summary>Query-string equivalent of <see cref="StoreHeader"/> for SignalR.</summary>
    public const string StoreQuery = "store";
}
