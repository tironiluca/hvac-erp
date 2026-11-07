using HvacErp.Application.Dtos;

namespace HvacErp.Application.Interfaces;

/// <summary>Installation scheduling use cases, including calendar fan-out.</summary>
public interface IInstallationService
{
    /// <summary>Installations ordered by date, optionally within an inclusive window.</summary>
    Task<List<ScheduledInstallationDto>> GetListAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
    /// <summary>Gets one installation.</summary>
    Task<ScheduledInstallationDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>Schedules a confirmed quote (one per quote, not in the past) and notifies calendar viewers.</summary>
    Task<ScheduledInstallationDto> CreateAsync(UpsertScheduledInstallationRequest request, CancellationToken cancellationToken = default);
    /// <summary>Full edit of an installation; rotates its concurrency token and notifies viewers.</summary>
    Task<ScheduledInstallationDto> UpdateAsync(int id, UpsertScheduledInstallationRequest request, CancellationToken cancellationToken = default);

    /// <summary>Drag-and-drop reschedule from the shared calendar — date / slot / crew only, with optimistic-concurrency checking.</summary>
    Task<ScheduledInstallationDto> RescheduleAsync(int id, RescheduleInstallationRequest request, CancellationToken cancellationToken = default);

    /// <summary>Cancels an installation and notifies calendar viewers.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
