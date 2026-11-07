using HvacErp.Application.Dtos;

namespace HvacErp.Application.Interfaces;

/// <summary>Repair / revision use cases.</summary>
public interface IRepairService
{
    /// <summary>Repairs newest first, optionally for one client.</summary>
    Task<List<RepairListItemDto>> GetListAsync(int? clientId = null, CancellationToken cancellationToken = default);
    /// <summary>Gets the full repair.</summary>
    Task<RepairDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>Creates a repair or revision, snapshotting service-item prices.</summary>
    Task<RepairDetailDto> CreateAsync(UpsertRepairRequest request, CancellationToken cancellationToken = default);
    /// <summary>Replaces the repair's content and service lines.</summary>
    Task<RepairDetailDto> UpdateAsync(int id, UpsertRepairRequest request, CancellationToken cancellationToken = default);
    /// <summary>Sets the status and appends a stamped history event.</summary>
    Task ChangeStatusAsync(int id, ChangeRepairStatusRequest request, CancellationToken cancellationToken = default);
    /// <summary>Books an appointment for the repair.</summary>
    Task<RepairScheduleEntryDto> AddScheduleEntryAsync(int id, AddRepairScheduleEntryRequest request, CancellationToken cancellationToken = default);
    /// <summary>Deletes a repair with its service lines, appointments, status history and F-Gas records.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
