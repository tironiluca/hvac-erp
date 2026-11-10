using HvacErp.Application.Dtos;

namespace HvacErp.Application.Interfaces;

/// <summary>
/// What the platform administrator manages: the tenants themselves. It works across tenants by
/// definition, so its implementation lives next to the database context rather than among the
/// Application services, which never leave the caller's tenant.
/// </summary>
public interface IPlatformTenantService
{
    /// <summary>Every tenant, suspended ones included.</summary>
    Task<List<TenantDto>> GetTenantsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a tenant with its first store, the defaults its quotes need and its first Admin, as
    /// one unit of work. Someone with no account is invited and lands on
    /// <paramref name="inviteRedirectTo"/> to choose a password.
    /// </summary>
    Task<TenantDto> CreateTenantAsync(CreateTenantRequest request, string? inviteRedirectTo, CancellationToken cancellationToken = default);

    /// <summary>Edits a tenant, or suspends / resumes it. The tenant being worked in cannot be suspended.</summary>
    Task<TenantDto> UpdateTenantAsync(int id, UpdateTenantRequest request, CancellationToken cancellationToken = default);
}
