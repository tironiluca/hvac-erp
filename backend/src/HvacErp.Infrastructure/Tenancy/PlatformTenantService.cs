using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;
using HvacErp.Application.Services;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;
using HvacErp.Infrastructure.Persistence;
using HvacErp.Infrastructure.Persistence.Seed;

namespace HvacErp.Infrastructure.Tenancy;

/// <summary>
/// Default <see cref="IPlatformTenantService"/>. The one place that deliberately steps outside the
/// caller's tenant: it lists every tenant and writes into a tenant the caller is not a member of,
/// through the context's explicit scopes.
/// </summary>
public class PlatformTenantService(
    AppDbContext context,
    ITenantContext requestTenant,
    IIdentityInviter inviter,
    ICurrentUser currentUser) : IPlatformTenantService
{
    /// <inheritdoc />
    public async Task<List<TenantDto>> GetTenantsAsync(CancellationToken cancellationToken = default)
    {
        using var system = context.AsSystem();
        return await context.Tenants.AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TenantDto(
                t.Id, t.Name, t.LegalName, t.VatNumber, t.IsActive, t.CreatedAt, t.Stores.Count, t.Memberships.Count))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TenantDto> CreateTenantAsync(CreateTenantRequest request, string? inviteRedirectTo, CancellationToken cancellationToken = default)
    {
        var storeName = request.FirstStoreName?.Trim();
        if (string.IsNullOrEmpty(storeName))
        {
            throw new BusinessRuleException("A company needs a first store.");
        }

        var tenant = new Tenant();
        await ApplyAsync(tenant, request.Name, request.LegalName, request.VatNumber, cancellationToken);

        // One unit of work on a real database: a company without its store, defaults or Admin is
        // of no use to anyone. (The in-memory provider used in tests has no transactions.)
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        Store store;
        using (context.AsSystem())
        {
            context.Tenants.Add(tenant);
            await context.SaveChangesAsync(cancellationToken);

            store = new Store { TenantId = tenant.Id, Name = storeName };
            context.Stores.Add(store);
            await context.SaveChangesAsync(cancellationToken);
        }

        // From here on the context works for the new tenant, so everything is stamped with it and
        // the ordinary tenant-level code can be reused as is.
        using (context.AsTenant(tenant.Id, store.Id))
        {
            await DevDataSeeder.SeedTenantDefaultsAsync(context, cancellationToken);

            // The invitation goes last: it sends an e-mail, which no rollback takes back.
            await new TenantAdministrationService(context, new Provisioning(currentUser.Name), inviter)
                .AddMemberAsync(new AddMemberRequest(request.AdminEmail, UserRole.Admin, AllStores: true), inviteRedirectTo, cancellationToken);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new TenantDto(tenant.Id, tenant.Name, tenant.LegalName, tenant.VatNumber, tenant.IsActive, tenant.CreatedAt, Stores: 1, Members: 1);
    }

    /// <inheritdoc />
    public async Task<TenantDto> UpdateTenantAsync(int id, UpdateTenantRequest request, CancellationToken cancellationToken = default)
    {
        using var system = context.AsSystem();
        var tenant = await context.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), id);

        // The tenant of the request itself (the context is in the system scope here): suspending it
        // would cut the administrator off in the middle of their own session.
        if (tenant.IsActive && !request.IsActive && id == requestTenant.TenantId)
        {
            throw new BusinessRuleException("You are working in this company. Switch to another one before suspending it.");
        }

        await ApplyAsync(tenant, request.Name, request.LegalName, request.VatNumber, cancellationToken);
        tenant.IsActive = request.IsActive;
        await context.SaveChangesAsync(cancellationToken);

        return await context.Tenants.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TenantDto(
                t.Id, t.Name, t.LegalName, t.VatNumber, t.IsActive, t.CreatedAt, t.Stores.Count, t.Memberships.Count))
            .SingleAsync(cancellationToken);
    }

    private async Task ApplyAsync(Tenant tenant, string? name, string? legalName, string? vatNumber, CancellationToken cancellationToken)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new BusinessRuleException("A company needs a name.");
        }

        if (await context.Tenants.AnyAsync(t => t.Id != tenant.Id && t.Name.ToLower() == trimmed.ToLower(), cancellationToken))
        {
            throw new ConflictException($"A company called '{trimmed}' already exists.");
        }

        tenant.Name = trimmed;
        tenant.LegalName = string.IsNullOrWhiteSpace(legalName) ? null : legalName.Trim();
        tenant.VatNumber = string.IsNullOrWhiteSpace(vatNumber) ? null : vatNumber.Trim();
    }

    /// <summary>
    /// The identity the new tenant's first membership is created under: no id, i.e. trusted system
    /// work (the platform administrator holds no role in a tenant that does not exist yet), with the
    /// administrator's name kept for audit stamps.
    /// </summary>
    private sealed class Provisioning(string? name) : ICurrentUser
    {
        public string? Id => null;
        public string? Name => name;
        public UserRole? Role => null;
    }
}
