using HvacErp.Application.Dtos;

namespace HvacErp.Application.Interfaces;

/// <summary>Client registry use cases.</summary>
public interface IClientService
{
    /// <summary>Searches by name, code, phone or mobile (max 100 rows).</summary>
    Task<List<ClientDto>> SearchAsync(string? search = null, CancellationToken cancellationToken = default);
    /// <summary>Gets one client.</summary>
    Task<ClientDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>Creates a client.</summary>
    Task<ClientDto> CreateAsync(UpsertClientRequest request, CancellationToken cancellationToken = default);
    /// <summary>Updates a client.</summary>
    Task<ClientDto> UpdateAsync(int id, UpsertClientRequest request, CancellationToken cancellationToken = default);
    /// <summary>
    /// Hard-deletes a client that has no quotes or repairs (refused otherwise, so business history is
    /// never wiped by accident). Clients it referred keep existing with the referral link cleared.
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
