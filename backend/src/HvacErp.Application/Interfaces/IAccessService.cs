using HvacErp.Application.Dtos;

namespace HvacErp.Application.Interfaces;

/// <summary>What the signed-in caller may work in.</summary>
public interface IAccessService
{
    /// <summary>The caller's profile: current tenant, role and store, and every tenant / store they could switch to.</summary>
    Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}
