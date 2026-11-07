using HvacErp.Application.Dtos;

namespace HvacErp.Application.Interfaces;

/// <summary>Read-only aggregates for the home dashboard.</summary>
public interface IDashboardService
{
    /// <summary>
    /// Builds the dashboard. <paramref name="weekStart"/> is the first calendar day of the week to list
    /// installations for (the SPA sends its local Monday as a date-only value); null means the
    /// current UTC week starting Monday.
    /// </summary>
    Task<DashboardDto> GetAsync(DateTime? weekStart, CancellationToken cancellationToken = default);
}
