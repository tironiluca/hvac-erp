using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;

namespace HvacErp.Application.Services;

/// <summary>Default <see cref="IAccessService"/>.</summary>
public class AccessService(IAppDbContext context, ICurrentUser currentUser, ITenantContext tenant) : IAccessService
{
    /// <inheritdoc />
    public async Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(currentUser.Id, out var userId))
        {
            return new CurrentUserDto(currentUser.Name ?? string.Empty, null, false, null, null, []);
        }

        // The caller's own grants in every tenant, not only the current one: the tenant filter is
        // lifted, the user-id predicate replaces it.
        var memberships = await context.TenantMemberships.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.UserId == userId && m.User.IsActive && m.Tenant.IsActive)
            .OrderBy(m => m.Tenant.Name)
            .Select(m => new
            {
                m.TenantId,
                TenantName = m.Tenant.Name,
                m.Role,
                m.AllStores,
                GrantedStoreIds = m.Stores.Select(s => s.StoreId).ToList(),
            })
            .ToListAsync(cancellationToken);

        var tenantIds = memberships.Select(m => m.TenantId).ToList();
        var stores = await context.Stores.IgnoreQueryFilters().AsNoTracking()
            .Where(s => tenantIds.Contains(s.TenantId) && s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new { s.TenantId, Summary = new StoreSummaryDto(s.Id, s.Name, s.City) })
            .ToListAsync(cancellationToken);

        var tenants = memberships
            .Select(m => new TenantAccessDto(
                m.TenantId,
                m.TenantName,
                m.Role.ToString(),
                stores.Where(s => s.TenantId == m.TenantId && (m.AllStores || m.GrantedStoreIds.Contains(s.Summary.Id)))
                    .Select(s => s.Summary)
                    .ToList()))
            .ToList();

        var isPlatformAdmin = await context.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => u.IsPlatformAdmin)
            .FirstOrDefaultAsync(cancellationToken);

        return new CurrentUserDto(
            currentUser.Name ?? string.Empty,
            tenant.TenantId is null ? null : currentUser.Role?.ToString(),
            isPlatformAdmin,
            tenant.TenantId,
            tenant.StoreId,
            tenants);
    }
}
