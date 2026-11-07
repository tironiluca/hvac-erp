using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;
using HvacErp.Domain.Entities;

namespace HvacErp.Application.Services;

/// <summary>Default <see cref="Interfaces.IRepairService"/> over EF Core.</summary>
public class RepairService(IAppDbContext context, ICurrentUser currentUser) : IRepairService
{
    public async Task<List<RepairListItemDto>> GetListAsync(int? clientId = null, CancellationToken cancellationToken = default)
    {
        var query = context.Repairs
            .Include(r => r.Client)
            .Include(r => r.ServiceItems)
            .AsQueryable();

        if (clientId is not null)
        {
            query = query.Where(r => r.ClientId == clientId);
        }

        var repairs = await query.OrderByDescending(r => r.InsertedAt).ToListAsync(cancellationToken);

        return repairs.Select(r => new RepairListItemDto(
            r.Id, r.Type, r.Status, r.ClientId, r.Client.FullName, r.Brand, r.Description,
            r.Urgent, r.InsertedAt, ComputeTotal(r))).ToList();
    }

    public async Task<RepairDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var repair = await LoadDetailedAsync(id, cancellationToken);
        return ToDetailDto(repair);
    }

    public async Task<RepairDetailDto> CreateAsync(UpsertRepairRequest request, CancellationToken cancellationToken = default)
    {
        _ = await context.Clients.FindAsync([request.ClientId], cancellationToken)
            ?? throw new NotFoundException(nameof(Client), request.ClientId);

        var repair = new Repair { ClientId = request.ClientId };
        await ApplyRequestAsync(repair, request, cancellationToken);

        context.Repairs.Add(repair);
        await context.SaveChangesAsync(cancellationToken);

        var reloaded = await LoadDetailedAsync(repair.Id, cancellationToken);
        return ToDetailDto(reloaded);
    }

    public async Task<RepairDetailDto> UpdateAsync(int id, UpsertRepairRequest request, CancellationToken cancellationToken = default)
    {
        var repair = await context.Repairs
            .Include(r => r.ServiceItems)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Repair), id);

        context.RepairServiceItems.RemoveRange(repair.ServiceItems);
        repair.ServiceItems.Clear();

        await ApplyRequestAsync(repair, request, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var reloaded = await LoadDetailedAsync(id, cancellationToken);
        return ToDetailDto(reloaded);
    }

    public async Task ChangeStatusAsync(int id, ChangeRepairStatusRequest request, CancellationToken cancellationToken = default)
    {
        var repair = await context.Repairs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Repair), id);

        repair.Status = request.Status;
        repair.StatusEvents.Add(new RepairStatusEvent
        {
            Status = request.Status,
            OccurredAt = DateTime.UtcNow,
            ChangedBy = currentUser.Name
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<RepairScheduleEntryDto> AddScheduleEntryAsync(int id, AddRepairScheduleEntryRequest request, CancellationToken cancellationToken = default)
    {
        var repair = await context.Repairs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Repair), id);

        var entry = new RepairScheduleEntry
        {
            RepairId = repair.Id,
            InstallerId = request.InstallerId,
            Date = request.Date,
            Notes = request.Notes
        };

        context.RepairScheduleEntries.Add(entry);
        await context.SaveChangesAsync(cancellationToken);

        var installerName = request.InstallerId is not null
            ? await context.Installers.Where(i => i.Id == request.InstallerId).Select(i => i.Name).FirstOrDefaultAsync(cancellationToken)
            : null;

        return new RepairScheduleEntryDto(entry.Id, entry.InstallerId, installerName, entry.Date, entry.Notes);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var repair = await context.Repairs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Repair), id);

        // Children (service lines, appointments, status events, F-Gas records) cascade in the database.
        context.Repairs.Remove(repair);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Repair> LoadDetailedAsync(int id, CancellationToken cancellationToken) =>
        await context.Repairs
            .Include(r => r.Client)
            .Include(r => r.ServiceItems).ThenInclude(s => s.ServiceItem)
            .Include(r => r.ScheduleEntries).ThenInclude(s => s.Installer)
            .Include(r => r.StatusEvents)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Repair), id);

    private async Task ApplyRequestAsync(Repair repair, UpsertRepairRequest request, CancellationToken cancellationToken)
    {
        repair.QuoteId = request.QuoteId;
        repair.Type = request.Type;
        repair.Brand = request.Brand;
        repair.Description = request.Description;
        repair.Urgent = request.Type == Domain.Enums.RepairType.Revision ? false : request.Urgent;
        repair.Predisposed = request.Type == Domain.Enums.RepairType.Revision ? true : request.Predisposed;
        repair.AllInclusive = request.AllInclusive;
        repair.UnitCount = request.UnitCount;
        repair.SplitCount = request.SplitCount;
        repair.Rounding = request.Rounding;
        repair.InstallationDate = request.InstallationDate;
        repair.FloorPlanSketchJson = FloorPlanSketchValidator.Validate(request.FloorPlanSketch);

        foreach (var s in request.ServiceItems)
        {
            var serviceItem = await context.ServiceItems.FindAsync([s.ServiceItemId], cancellationToken)
                ?? throw new NotFoundException(nameof(ServiceItem), s.ServiceItemId);

            repair.ServiceItems.Add(new RepairServiceItem
            {
                ServiceItemId = s.ServiceItemId,
                Quantity = s.Quantity,
                UnitPrice = serviceItem.Price
            });
        }
    }

    private static decimal ComputeTotal(Repair r) =>
        r.ServiceItems.Sum(s => s.Quantity * s.UnitPrice) + r.Rounding;

    private static RepairDetailDto ToDetailDto(Repair r) => new(
        r.Id, r.Type, r.Status, r.ClientId, r.Client.FullName, r.QuoteId, r.Brand, r.Description,
        r.Urgent, r.Predisposed, r.AllInclusive, r.UnitCount, r.SplitCount, r.Rounding,
        r.InsertedAt, r.InstallationDate, r.PdfUrl, ComputeTotal(r),
        r.ServiceItems.Select(s => new RepairServiceItemDto(s.Id, s.ServiceItemId, s.ServiceItem.Name, s.Quantity, s.UnitPrice)).ToList(),
        r.ScheduleEntries.Select(s => new RepairScheduleEntryDto(s.Id, s.InstallerId, s.Installer?.Name, s.Date, s.Notes)).ToList(),
        r.StatusEvents.OrderBy(e => e.OccurredAt)
            .Select(e => new RepairStatusEventDto(e.Id, e.Status, e.OccurredAt, e.Notes, e.ChangedBy)).ToList(),
        FloorPlanSketchJson.Parse(r.FloorPlanSketchJson));
}
