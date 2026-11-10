using HvacErp.Application.Dtos;

namespace HvacErp.Application.Interfaces;

/// <summary>What a tenant's Admin manages about their own company: its stores and its members.</summary>
public interface ITenantAdministrationService
{
    /// <summary>Every store of the current tenant, inactive ones included.</summary>
    Task<List<StoreDto>> GetStoresAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds a store. 409 when the name is taken in this tenant.</summary>
    Task<StoreDto> CreateStoreAsync(UpsertStoreRequest request, CancellationToken cancellationToken = default);

    /// <summary>Edits a store. The tenant's last active store cannot be deactivated.</summary>
    Task<StoreDto> UpdateStoreAsync(int id, UpsertStoreRequest request, CancellationToken cancellationToken = default);

    /// <summary>The current tenant's members.</summary>
    Task<List<MemberDto>> GetMembersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes the person behind an e-mail a member. Someone without an account is invited and lands
    /// on <paramref name="inviteRedirectTo"/> to choose a password.
    /// </summary>
    Task<MemberDto> AddMemberAsync(AddMemberRequest request, string? inviteRedirectTo, CancellationToken cancellationToken = default);

    /// <summary>Changes a member's role and stores. The tenant always keeps at least one Admin.</summary>
    Task<MemberDto> UpdateMemberAsync(int membershipId, UpdateMemberRequest request, CancellationToken cancellationToken = default);

    /// <summary>Removes a member from the tenant; their account and other memberships are untouched.</summary>
    Task RemoveMemberAsync(int membershipId, CancellationToken cancellationToken = default);
}
