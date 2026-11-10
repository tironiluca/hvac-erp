using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;
using HvacErp.Infrastructure.Persistence;

namespace HvacErp.Infrastructure.Auth;

/// <summary>What a signed-in identity may do in the tenant and store a request asked for.</summary>
/// <param name="UserId">The <see cref="AppUser"/> id.</param>
/// <param name="IsPlatformAdmin">May manage tenants and platform-wide settings.</param>
/// <param name="TenantId">The tenant the request works in; null when access is not granted.</param>
/// <param name="Role">The caller's role in that tenant.</param>
/// <param name="StoreIds">The stores the request reads.</param>
/// <param name="ActiveStoreId">The store new store-level rows go to, when unambiguous.</param>
public sealed record ResolvedAccess(
    Guid UserId,
    bool IsPlatformAdmin,
    int? TenantId,
    UserRole? Role,
    IReadOnlyList<int> StoreIds,
    int? ActiveStoreId)
{
    /// <summary>A known person with no access to any tenant.</summary>
    public static ResolvedAccess NoTenant(Guid userId, bool isPlatformAdmin) =>
        new(userId, isPlatformAdmin, null, null, [], null);
}

/// <summary>
/// Turns a Supabase identity plus the tenant / store a request selected into what that request may
/// touch. Access is granted, never assumed: an identity seen for the first time gets a profile row
/// and nothing else; only a <see cref="TenantMembership"/> opens a tenant.
/// </summary>
public class TenantAccessResolver(AppDbContext context, IConfiguration configuration)
{
    /// <summary>Configuration key of the e-mail that becomes the first administrator.</summary>
    public const string BootstrapAdminEmailKey = "Bootstrap:AdminEmail";

    /// <summary>
    /// Resolves the access of <paramref name="userId"/>. A requested tenant or store the user may not
    /// use yields no tenant at all rather than a silent fallback to another one.
    /// </summary>
    public async Task<ResolvedAccess> ResolveAsync(
        Guid userId, string? email, int? requestedTenantId, int? requestedStoreId, CancellationToken cancellationToken = default)
    {
        // Sign-in resolution runs before the request has a tenant, and reads across tenants.
        using var system = context.AsSystem();

        var user = await context.Users
            .Include(u => u.Memberships).ThenInclude(m => m.Stores)
            .Include(u => u.Memberships).ThenInclude(m => m.Tenant)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            user = new AppUser { Id = userId, Email = email ?? string.Empty, DisplayName = email ?? string.Empty };
            context.Users.Add(user);
            await context.SaveChangesAsync(cancellationToken);
        }

        await BootstrapFirstAdminAsync(user, cancellationToken);

        if (!user.IsActive)
        {
            return ResolvedAccess.NoTenant(user.Id, isPlatformAdmin: false);
        }

        var memberships = user.Memberships.Where(m => m.Tenant.IsActive).OrderBy(m => m.TenantId).ToList();
        var membership = requestedTenantId is { } tenantId
            ? memberships.FirstOrDefault(m => m.TenantId == tenantId)
            : memberships.FirstOrDefault();
        if (membership is null)
        {
            return ResolvedAccess.NoTenant(user.Id, user.IsPlatformAdmin);
        }

        var stores = membership.AllStores
            ? await context.Stores.Where(s => s.TenantId == membership.TenantId).OrderBy(s => s.Id).Select(s => s.Id).ToListAsync(cancellationToken)
            : membership.Stores.Select(s => s.StoreId).Order().ToList();

        if (requestedStoreId is { } storeId)
        {
            return stores.Contains(storeId)
                ? new ResolvedAccess(user.Id, user.IsPlatformAdmin, membership.TenantId, membership.Role, [storeId], storeId)
                : ResolvedAccess.NoTenant(user.Id, user.IsPlatformAdmin);
        }

        return new ResolvedAccess(
            user.Id, user.IsPlatformAdmin, membership.TenantId, membership.Role, stores,
            stores.Count == 1 ? stores[0] : null);
    }

    /// <summary>
    /// A fresh deployment has nobody who could grant access. The identity whose e-mail matches
    /// <see cref="BootstrapAdminEmailKey"/> becomes platform administrator and Admin of the first
    /// tenant — only while no platform administrator exists, so the setting is inert afterwards.
    /// </summary>
    private async Task BootstrapFirstAdminAsync(AppUser user, CancellationToken cancellationToken)
    {
        var bootstrapEmail = configuration[BootstrapAdminEmailKey];
        if (string.IsNullOrWhiteSpace(bootstrapEmail)
            || !string.Equals(bootstrapEmail.Trim(), user.Email, StringComparison.OrdinalIgnoreCase)
            || await context.Users.AnyAsync(u => u.IsPlatformAdmin, cancellationToken))
        {
            return;
        }

        var tenant = await context.Tenants.OrderBy(t => t.Id).FirstOrDefaultAsync(cancellationToken);
        if (tenant is null)
        {
            return;
        }

        user.IsPlatformAdmin = true;
        user.IsActive = true;
        var membership = user.Memberships.FirstOrDefault(m => m.TenantId == tenant.Id);
        if (membership is null)
        {
            membership = new TenantMembership { TenantId = tenant.Id, UserId = user.Id, Tenant = tenant };
            user.Memberships.Add(membership);
        }
        membership.Role = UserRole.Admin;
        membership.AllStores = true;
        await context.SaveChangesAsync(cancellationToken);
    }
}
