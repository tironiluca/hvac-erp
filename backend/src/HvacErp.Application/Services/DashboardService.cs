using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;
using HvacErp.Domain.Enums;

namespace HvacErp.Application.Services;

/// <summary>Default <see cref="IDashboardService"/>: a handful of small aggregate queries, no tracking.</summary>
public class DashboardService(IAppDbContext context, TimeProvider clock) : IDashboardService
{
    private const int OldestOpenCount = 5;
    private const int ActivityCount = 10;

    /// <inheritdoc />
    public async Task<DashboardDto> GetAsync(DateTime? weekStart, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var from = (weekStart ?? MondayOf(now)).Date;
        var to = from.AddDays(7);

        return new DashboardDto(
            DateTime.SpecifyKind(from, DateTimeKind.Utc),
            await GetQuotePipelineAsync(now, cancellationToken),
            await GetOpenRepairsByStatusAsync(cancellationToken),
            await CountUrgentOpenRepairsAsync(cancellationToken),
            await GetWeekInstallationsAsync(from, to, cancellationToken),
            await GetRecentActivityAsync(cancellationToken));
    }

    private static DateTime MondayOf(DateTime date) =>
        date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private async Task<QuotePipelineDto> GetQuotePipelineAsync(DateTime now, CancellationToken cancellationToken)
    {
        var byStatus = await context.Quotes.AsNoTracking()
            .GroupBy(q => q.CurrentStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        var since = now.AddDays(-30);
        var confirmedRecently = await context.QuoteStatusEvents.AsNoTracking()
            .Where(e => e.Status == QuoteStatusCodes.Confirmed && e.OccurredAt >= since)
            .Select(e => e.QuoteId)
            .Distinct()
            .CountAsync(cancellationToken);

        var oldestOpen = await context.Quotes.AsNoTracking()
            .Include(q => q.Client)
            .Where(q => q.CurrentStatus != QuoteStatusCodes.Confirmed)
            .OrderBy(q => q.CreatedAt)
            .Take(OldestOpenCount)
            .ToListAsync(cancellationToken);

        return new QuotePipelineDto(
            byStatus.GetValueOrDefault(QuoteStatusCodes.Draft),
            byStatus.GetValueOrDefault(QuoteStatusCodes.SurveyDone),
            byStatus.GetValueOrDefault(QuoteStatusCodes.Recalled),
            confirmedRecently,
            oldestOpen.Select(q => q.ToListItemDto()).ToList());
    }

    private async Task<List<StatusCountDto>> GetOpenRepairsByStatusAsync(CancellationToken cancellationToken)
    {
        var openStatuses = await context.RepairStatuses.AsNoTracking()
            .Where(s => s.Active && !s.IsTerminal)
            .OrderBy(s => s.SortOrder)
            .Select(s => s.Code)
            .ToListAsync(cancellationToken);

        var counts = await context.Repairs.AsNoTracking()
            .Where(r => openStatuses.Contains(r.Status))
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        return openStatuses.Select(s => new StatusCountDto(s, counts.GetValueOrDefault(s))).ToList();
    }

    private async Task<int> CountUrgentOpenRepairsAsync(CancellationToken cancellationToken)
    {
        var terminal = context.RepairStatuses.Where(s => s.IsTerminal).Select(s => s.Code);
        return await context.Repairs.AsNoTracking()
            .CountAsync(r => r.Urgent && !terminal.Contains(r.Status), cancellationToken);
    }

    private async Task<List<DashboardInstallationDto>> GetWeekInstallationsAsync(DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var slotOrder = await context.DaySlots.AsNoTracking()
            .ToDictionaryAsync(s => s.Code, s => s.SortOrder, cancellationToken);

        var installations = await context.ScheduledInstallations.AsNoTracking()
            .Include(s => s.Quote).ThenInclude(q => q.Client)
            .Include(s => s.Installers).ThenInclude(i => i.Installer)
            .Where(s => s.Date >= from && s.Date < to)
            .ToListAsync(cancellationToken);

        return installations
            .OrderBy(s => s.Date)
            .ThenBy(s => slotOrder.GetValueOrDefault(s.Slot))
            .Select(s => new DashboardInstallationDto(
                s.Id,
                s.QuoteId,
                s.Quote.DisplayNumber(),
                s.Quote.Client.FullName,
                s.Quote.InstallationCity ?? s.Quote.Client.City,
                s.Date,
                s.Slot,
                s.Installers.Select(i => i.Installer.Name).OrderBy(n => n).ToList()))
            .ToList();
    }

    private async Task<List<ActivityItemDto>> GetRecentActivityAsync(CancellationToken cancellationToken)
    {
        var quoteEvents = await context.QuoteStatusEvents.AsNoTracking()
            .OrderByDescending(e => e.OccurredAt)
            .Take(ActivityCount)
            .Select(e => new
            {
                e.QuoteId, e.Quote.Number, e.Quote.Version, e.Quote.Revision,
                Client = e.Quote.Client.FullName, e.Status, e.OccurredAt, e.ChangedBy,
            })
            .ToListAsync(cancellationToken);

        var repairEvents = await context.RepairStatusEvents.AsNoTracking()
            .OrderByDescending(e => e.OccurredAt)
            .Take(ActivityCount)
            .Select(e => new { e.RepairId, Client = e.Repair.Client.FullName, e.Status, e.OccurredAt, e.ChangedBy })
            .ToListAsync(cancellationToken);

        return quoteEvents
            .Select(e => new ActivityItemDto("Quote", e.QuoteId,
                e.Revision > 0 ? $"{e.Number}/{e.Version}.{e.Revision}" : $"{e.Number}/{e.Version}",
                e.Client, e.Status, e.OccurredAt, e.ChangedBy))
            .Concat(repairEvents.Select(e => new ActivityItemDto("Repair", e.RepairId, $"#{e.RepairId}",
                e.Client, e.Status, e.OccurredAt, e.ChangedBy)))
            .OrderByDescending(a => a.OccurredAt)
            .Take(ActivityCount)
            .ToList();
    }
}
