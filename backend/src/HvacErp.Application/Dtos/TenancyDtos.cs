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
