using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;

namespace HvacErp.Application.Services;

/// <summary>
/// Default <see cref="ITenantAdministrationService"/>. Everything here runs inside the caller's
/// tenant: the context's filters and write guard make another tenant's stores and members
/// unreachable, so no method takes a tenant id.
/// </summary>
public class TenantAdministrationService(
    IAppDbContext context,
    ICurrentUser currentUser,
    IIdentityInviter inviter) : ITenantAdministrationService
{
    private const string Operation = "manage the company's stores and members";

    /// <inheritdoc />
    public async Task<List<StoreDto>> GetStoresAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        return await context.Stores.AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new StoreDto(s.Id, s.Name, s.Address, s.City, s.Phone, s.Email, s.IsActive))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<StoreDto> CreateStoreAsync(UpsertStoreRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var store = new Store();
        await ApplyAsync(store, request, cancellationToken);
        context.Stores.Add(store);
        await context.SaveChangesAsync(cancellationToken);
        return ToDto(store);
    }

    /// <inheritdoc />
    public async Task<StoreDto> UpdateStoreAsync(int id, UpsertStoreRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var store = await context.Stores.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Store), id);

        if (store.IsActive && !request.IsActive
            && !await context.Stores.AnyAsync(s => s.Id != id && s.IsActive, cancellationToken))
        {
            throw new BusinessRuleException("A company needs at least one active store.");
        }

        await ApplyAsync(store, request, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return ToDto(store);
    }

    /// <inheritdoc />
    public async Task<List<MemberDto>> GetMembersAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var members = await MembersQuery().AsNoTracking().OrderBy(m => m.User.Email).ToListAsync(cancellationToken);
        return members.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<MemberDto> AddMemberAsync(AddMemberRequest request, string? inviteRedirectTo, CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var email = NormalizeEmail(request.Email);
        var stores = await ResolveStoresAsync(request.AllStores, request.StoreIds, cancellationToken);
        await EnsureInstallerAsync(request.InstallerId, cancellationToken);

        var user = await context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email, cancellationToken);
        if (user is not null && await context.TenantMemberships.AnyAsync(m => m.UserId == user.Id, cancellationToken))
        {
            throw new ConflictException($"{email} is already a member of this company.");
        }

        if (user is null)
        {
            // No profile: either the person has no account at all, or one that never signed in here.
            // The provider creates the account and sends the link; an existing account is a conflict
            // it reports, and signing in once is what gives that person a profile to add.
            Guid accountId;
            try
            {
                accountId = await inviter.InviteAsync(email, inviteRedirectTo, cancellationToken);
            }
            catch (ConflictException)
            {
                throw new ConflictException(
                    $"{email} already has an account but has never signed in here. Ask them to sign in once, then add them again.");
            }

            user = new AppUser { Id = accountId, Email = email, DisplayName = email };
            context.Users.Add(user);
        }

        var membership = new TenantMembership
        {
            User = user,
            Role = request.Role,
            AllStores = request.AllStores,
            InstallerId = request.InstallerId,
        };
        foreach (var storeId in stores)
        {
            membership.Stores.Add(new StoreAccess { StoreId = storeId });
        }

        context.TenantMemberships.Add(membership);
        await context.SaveChangesAsync(cancellationToken);
        return ToDto(membership);
    }

    /// <inheritdoc />
    public async Task<MemberDto> UpdateMemberAsync(int membershipId, UpdateMemberRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var membership = await MembersQuery().FirstOrDefaultAsync(m => m.Id == membershipId, cancellationToken)
            ?? throw new NotFoundException("Member", membershipId);
        var stores = await ResolveStoresAsync(request.AllStores, request.StoreIds, cancellationToken);
        await EnsureInstallerAsync(request.InstallerId, cancellationToken);

        if (membership.Role == UserRole.Admin && request.Role != UserRole.Admin)
        {
            await EnsureAnotherAdminAsync(membershipId, cancellationToken);
        }

        membership.Role = request.Role;
        membership.AllStores = request.AllStores;
        membership.InstallerId = request.InstallerId;

        foreach (var removed in membership.Stores.Where(s => !stores.Contains(s.StoreId)).ToList())
        {
            membership.Stores.Remove(removed);
        }
        foreach (var storeId in stores.Where(id => membership.Stores.All(s => s.StoreId != id)))
        {
            membership.Stores.Add(new StoreAccess { StoreId = storeId });
        }

        await context.SaveChangesAsync(cancellationToken);
        return ToDto(membership);
    }

    /// <inheritdoc />
    public async Task RemoveMemberAsync(int membershipId, CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        var membership = await MembersQuery().FirstOrDefaultAsync(m => m.Id == membershipId, cancellationToken)
            ?? throw new NotFoundException("Member", membershipId);

        if (membership.Role == UserRole.Admin)
        {
            await EnsureAnotherAdminAsync(membershipId, cancellationToken);
        }

        context.TenantMemberships.Remove(membership);
        await context.SaveChangesAsync(cancellationToken);
    }

    private void EnsureAdmin() => currentUser.EnsureInRole(Operation, UserRole.Admin);

    private IQueryable<TenantMembership> MembersQuery() =>
        context.TenantMemberships.Include(m => m.User).Include(m => m.Stores);

    /// <summary>A company that loses its last Admin can no longer be administered by anyone in it.</summary>
    private async Task EnsureAnotherAdminAsync(int membershipId, CancellationToken cancellationToken)
    {
        var another = await context.TenantMemberships
            .AnyAsync(m => m.Id != membershipId && m.Role == UserRole.Admin && m.User.IsActive, cancellationToken);
        if (!another)
        {
            throw new BusinessRuleException("A company needs at least one Admin. Make someone else Admin first.");
        }
    }

    /// <summary>The store grants to record: none for "all stores", otherwise ids checked against this tenant's stores.</summary>
    private async Task<List<int>> ResolveStoresAsync(bool allStores, List<int>? storeIds, CancellationToken cancellationToken)
    {
        if (allStores)
        {
            return [];
        }

        var requested = (storeIds ?? []).Distinct().ToList();
        if (requested.Count == 0)
        {
            throw new BusinessRuleException("Choose at least one store, or give access to all stores.");
        }

        // The filtered set only knows this tenant's stores: another tenant's id simply is not there.
        var known = await context.Stores.Where(s => requested.Contains(s.Id)).Select(s => s.Id).ToListAsync(cancellationToken);
        var unknown = requested.Except(known).ToList();
        if (unknown.Count > 0)
        {
            throw new NotFoundException(nameof(Store), string.Join(", ", unknown));
        }

        return requested;
    }

    private async Task EnsureInstallerAsync(int? installerId, CancellationToken cancellationToken)
    {
        if (installerId is { } id && !await context.Installers.AnyAsync(i => i.Id == id, cancellationToken))
        {
            throw new NotFoundException(nameof(Installer), id);
        }
    }

    private async Task ApplyAsync(Store store, UpsertStoreRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new BusinessRuleException("A store needs a name.");
        }

        if (await context.Stores.AnyAsync(s => s.Id != store.Id && s.Name.ToLower() == name.ToLower(), cancellationToken))
        {
            throw new ConflictException($"This company already has a store called '{name}'.");
        }

        store.Name = name;
        store.Address = Clean(request.Address);
        store.City = Clean(request.City);
        store.Phone = Clean(request.Phone);
        store.Email = Clean(request.Email);
        store.IsActive = request.IsActive;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeEmail(string? email)
    {
        var trimmed = email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!MailAddress.TryCreate(trimmed, out var parsed) || parsed.Address != trimmed)
        {
            throw new BusinessRuleException("Enter a valid e-mail address.");
        }
        return trimmed;
    }

    private static StoreDto ToDto(Store s) => new(s.Id, s.Name, s.Address, s.City, s.Phone, s.Email, s.IsActive);

    private MemberDto ToDto(TenantMembership m) => new(
        m.Id, m.UserId, m.User.Email, m.User.DisplayName, m.Role.ToString(), m.AllStores,
        m.Stores.Select(s => s.StoreId).Order().ToList(), m.InstallerId, m.User.IsActive,
        m.UserId.ToString() == currentUser.Id);
}
