using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HvacErp.Application.Common;

namespace HvacErp.Infrastructure.Persistence.Seed;

/// <summary>Builds the database context the seed commands write through.</summary>
public static class SeedContext
{
    /// <summary>
    /// A context working for one tenant and its first store, so the seeders' rows are stamped and
    /// their "already seeded?" checks look at that tenant only. Defaults to the first tenant (the one
    /// the migration creates); pass <paramref name="tenantId"/> to seed another.
    /// </summary>
    /// <exception cref="InvalidOperationException">The tenant does not exist or has no store.</exception>
    public static async Task<AppDbContext> CreateAsync(
        IServiceProvider services, int? tenantId = null, CancellationToken cancellationToken = default)
    {
        var options = services.GetRequiredService<DbContextOptions<AppDbContext>>();

        int resolvedTenantId;
        List<int> storeIds;
        await using (var lookup = new AppDbContext(options, FixedTenantContext.System))
        {
            resolvedTenantId = tenantId
                ?? await lookup.Tenants.OrderBy(t => t.Id).Select(t => (int?)t.Id).FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("No tenant exists. Apply the migrations first: they create the default tenant and store.");
            storeIds = await lookup.Stores.Where(s => s.TenantId == resolvedTenantId).OrderBy(s => s.Id).Select(s => s.Id).ToListAsync(cancellationToken);
        }

        if (storeIds.Count == 0)
        {
            throw new InvalidOperationException($"Tenant {resolvedTenantId} does not exist or has no store.");
        }

        return new AppDbContext(options, FixedTenantContext.For(resolvedTenantId, storeIds, storeIds[0]));
    }
}
