using HvacErp.Domain.Enums;

namespace HvacErp.Application.Dtos;

/// <summary>
/// The signed-in caller: who they are, what this request works in, and what else they could switch
/// to. <see cref="Role"/>, <see cref="TenantId"/> and <see cref="StoreId"/> are null for someone who
/// has not been given access to any company yet.
/// </summary>
/// <param name="Email">The caller's e-mail.</param>
/// <param name="Role">Their role in the current tenant (<c>Admin</c> / <c>Sales</c> / <c>Warehouse</c>).</param>
/// <param name="IsPlatformAdmin">May manage tenants and platform-wide settings.</param>
/// <param name="TenantId">The tenant this request works in.</param>
/// <param name="StoreId">The store new quotes and repairs go to; null when several are open and none is selected.</param>
/// <param name="Tenants">Every tenant the caller belongs to, with the stores they may work in.</param>
public record CurrentUserDto(
    string Email,
    string? Role,
    bool IsPlatformAdmin,
    int? TenantId,
    int? StoreId,
    IReadOnlyList<TenantAccessDto> Tenants);

/// <summary>A tenant the caller belongs to.</summary>
/// <param name="Id">Tenant id, sent back as <c>X-Tenant-Id</c> to work in it.</param>
/// <param name="Name">Display name.</param>
/// <param name="Role">The caller's role there.</param>
/// <param name="Stores">The active stores the caller may work in.</param>
public record TenantAccessDto(int Id, string Name, string Role, IReadOnlyList<StoreSummaryDto> Stores);

/// <summary>A store, as listed in the switcher.</summary>
/// <param name="Id">Store id, sent back as <c>X-Store-Id</c> to narrow a request to it.</param>
/// <param name="Name">Display name.</param>
/// <param name="City">Where it is, when recorded.</param>
public record StoreSummaryDto(int Id, string Name, string? City);

/// <summary>A store of the current tenant, as edited by its Admin.</summary>
public record StoreDto(int Id, string Name, string? Address, string? City, string? Phone, string? Email, bool IsActive);

/// <summary>Create / update payload for a store. Deactivating hides it from the switcher; its records stay.</summary>
public record UpsertStoreRequest(string Name, string? Address, string? City, string? Phone, string? Email, bool IsActive = true);

/// <summary>A member of the current tenant: the person, their role there and the stores they may work in.</summary>
/// <param name="Id">Membership id.</param>
/// <param name="UserId">The person's profile id.</param>
/// <param name="Email">Their sign-in e-mail.</param>
/// <param name="DisplayName">Their name as shown in the app.</param>
/// <param name="Role"><c>Admin</c> / <c>Sales</c> / <c>Warehouse</c> in this tenant.</param>
/// <param name="AllStores">True: every store, present and future.</param>
/// <param name="StoreIds">The stores granted when <paramref name="AllStores"/> is false.</param>
/// <param name="InstallerId">The installer record this person corresponds to, if any.</param>
/// <param name="IsActive">False when a platform administrator has disabled the person everywhere.</param>
/// <param name="IsYou">True for the caller's own membership.</param>
public record MemberDto(
    int Id,
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    bool AllStores,
    List<int> StoreIds,
    int? InstallerId,
    bool IsActive,
    bool IsYou);

/// <summary>
/// Adds someone to the current tenant by e-mail. A person who has no account yet is invited: they
/// receive a link to choose a password.
/// </summary>
public record AddMemberRequest(string Email, UserRole Role, bool AllStores, List<int>? StoreIds = null, int? InstallerId = null);

/// <summary>Changes a member's role and stores.</summary>
public record UpdateMemberRequest(UserRole Role, bool AllStores, List<int>? StoreIds = null, int? InstallerId = null);
