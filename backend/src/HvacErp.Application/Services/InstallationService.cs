using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;

namespace HvacErp.Application.Services;

/// <summary>Default <see cref="Interfaces.IInstallationService"/>: scheduling rules, optimistic concurrency and calendar fan-out.</summary>
public class InstallationService(
    IAppDbContext context,
    ICurrentUser currentUser,
    IInstallationCalendarNotifier notifier) : IInstallationService
{
    public async Task<List<ScheduledInstallationDto>> GetListAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var query = context.ScheduledInstallations
            .Include(s => s.Quote).ThenInclude(q => q.Client)
            .Include(s => s.Installers).ThenInclude(i => i.Installer)
            .AsQueryable();

        if (from is not null)
        {
            query = query.Where(s => s.Date >= from);
        }
        if (to is not null)
        {
            query = query.Where(s => s.Date <= to);
        }

        var installations = await query.OrderBy(s => s.Date).ToListAsync(cancellationToken);
        return installations.Select(ToDto).ToList();
    }

    public async Task<ScheduledInstallationDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var installation = await LoadDetailedAsync(id, cancellationToken);
        return ToDto(installation);
    }

    public async Task<ScheduledInstallationDto> CreateAsync(UpsertScheduledInstallationRequest request, CancellationToken cancellationToken = default)
    {
        var quote = await context.Quotes.FirstOrDefaultAsync(q => q.Id == request.QuoteId, cancellationToken)
            ?? throw new NotFoundException(nameof(Quote), request.QuoteId);

        if (quote.CurrentStatus != QuoteStatusCodes.Confirmed)
        {
            throw new BusinessRuleException("Only confirmed quotes can be scheduled for installation.");
        }

        ValidateNotInThePast(request.Date);

        var alreadyScheduled = await context.ScheduledInstallations.AnyAsync(s => s.QuoteId == request.QuoteId, cancellationToken);
        if (alreadyScheduled)
        {
            throw new BusinessRuleException("This quote already has a scheduled installation — update it instead of creating a new one.");
        }

        // An installation lives in its quote's store, whichever store the caller has selected.
        var installation = new ScheduledInstallation { QuoteId = request.QuoteId, StoreId = quote.StoreId };
        installation.Notes = request.Notes;
        installation.FloorPlanSketchJson = FloorPlanSketchValidator.Validate(request.FloorPlanSketch);
        await ApplyScheduleAsync(installation, request.Date, request.Slot, request.InstallerIds, cancellationToken);

        context.ScheduledInstallations.Add(installation);
        await context.SaveChangesAsync(cancellationToken);

        return await ReloadNotifyAsync(installation.Id, cancellationToken);
    }

    public async Task<ScheduledInstallationDto> UpdateAsync(int id, UpsertScheduledInstallationRequest request, CancellationToken cancellationToken = default)
    {
        var installation = await context.ScheduledInstallations
            .Include(s => s.Installers)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(ScheduledInstallation), id);

        ValidateNotInThePast(request.Date);

        installation.Notes = request.Notes;
        installation.FloorPlanSketchJson = FloorPlanSketchValidator.Validate(request.FloorPlanSketch);
        context.InstallationInstallers.RemoveRange(installation.Installers);
        installation.Installers.Clear();
        await ApplyScheduleAsync(installation, request.Date, request.Slot, request.InstallerIds, cancellationToken);

        await SaveGuardingConcurrencyAsync(cancellationToken);

        return await ReloadNotifyAsync(id, cancellationToken);
    }

    public async Task<ScheduledInstallationDto> RescheduleAsync(int id, RescheduleInstallationRequest request, CancellationToken cancellationToken = default)
    {
        var installation = await context.ScheduledInstallations
            .Include(s => s.Installers)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(ScheduledInstallation), id);

        // The caller moved a card it last saw as `RowVersion`; if the stored token has moved on,
        // someone else got there first — bail out so the UI can revert and refresh.
        if (!string.IsNullOrEmpty(request.RowVersion)
            && Guid.TryParse(request.RowVersion, out var seen)
            && seen != installation.ConcurrencyToken)
        {
            throw new ConflictException("This installation was changed by someone else — your calendar has been refreshed.");
        }

        ValidateNotInThePast(request.Date);

        context.InstallationInstallers.RemoveRange(installation.Installers);
        installation.Installers.Clear();
        await ApplyScheduleAsync(installation, request.Date, request.Slot, request.InstallerIds, cancellationToken);

        await SaveGuardingConcurrencyAsync(cancellationToken);

        return await ReloadNotifyAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var installation = await context.ScheduledInstallations.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(ScheduledInstallation), id);
        context.ScheduledInstallations.Remove(installation);
        await context.SaveChangesAsync(cancellationToken);

        await notifier.InstallationDeletedAsync(id, installation.StoreId, cancellationToken);
    }

    private static void ValidateNotInThePast(DateTime date)
    {
        if (date.Date < DateTime.UtcNow.Date)
        {
            throw new BusinessRuleException("Cannot schedule an installation in the past.");
        }
    }

    private async Task ApplyScheduleAsync(ScheduledInstallation installation, DateTime date, string slot, List<int> installerIds, CancellationToken cancellationToken)
    {
        installation.Date = date;
        installation.Slot = slot;

        foreach (var installerId in installerIds.Distinct())
        {
            _ = await context.Installers.FindAsync([installerId], cancellationToken)
                ?? throw new NotFoundException(nameof(Installer), installerId);

            installation.Installers.Add(new InstallationInstaller { InstallerId = installerId });
        }

        installation.ConcurrencyToken = Guid.NewGuid();
        installation.UpdatedAt = DateTime.UtcNow;
        installation.UpdatedBy = currentUser.Name;
    }

    private async Task SaveGuardingConcurrencyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("This installation was changed by someone else — your calendar has been refreshed.");
        }
    }

    private async Task<ScheduledInstallationDto> ReloadNotifyAsync(int id, CancellationToken cancellationToken)
    {
        var dto = ToDto(await LoadDetailedAsync(id, cancellationToken));
        await notifier.InstallationChangedAsync(dto, cancellationToken);
        return dto;
    }

    private async Task<ScheduledInstallation> LoadDetailedAsync(int id, CancellationToken cancellationToken) =>
        await context.ScheduledInstallations
            .Include(s => s.Quote).ThenInclude(q => q.Client)
            .Include(s => s.Installers).ThenInclude(i => i.Installer)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(ScheduledInstallation), id);

    private static ScheduledInstallationDto ToDto(ScheduledInstallation s) => new(
        s.Id,
        s.StoreId,
        s.QuoteId,
        s.Quote.DisplayNumber(),
        s.Quote.Client.FullName,
        s.Quote.InstallationCity ?? s.Quote.Client.City,
        s.Date,
        s.Slot,
        s.Notes,
        s.ReportPdfUrl,
        s.ConcurrencyToken.ToString(),
        s.UpdatedAt,
        s.UpdatedBy,
        s.Installers.Select(i => new InstallerSummaryDto(i.InstallerId, i.Installer.Name)).ToList(),
        FloorPlanSketchJson.Parse(s.FloorPlanSketchJson));
}
