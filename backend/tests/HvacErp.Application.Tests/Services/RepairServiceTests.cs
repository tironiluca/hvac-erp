using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Services;
using HvacErp.Application.Tests.Support;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;
using HvacErp.Infrastructure.Persistence;

namespace HvacErp.Application.Tests.Services;

public class RepairServiceTests
{
    private readonly AppDbContext _db = TestDb.Create();
    private readonly RepairService _sut;
    private readonly Client _client;
    private readonly ServiceItem _labour;

    public RepairServiceTests()
    {
        _client = TestDb.AddClient(_db);
        _labour = new ServiceItem { Name = "Hourly labour", Price = 45m };
        _db.ServiceItems.Add(_labour);
        _db.SaveChanges();
        _sut = new RepairService(_db, new FakeCurrentUser("tech@example.com"));
    }

    private UpsertRepairRequest Request(RepairType type = RepairType.Repair, bool urgent = true, bool predisposed = false) => new(
        ClientId: _client.Id,
        QuoteId: null,
        Type: type,
        Brand: "Daikin",
        Description: "Unit not cooling",
        Urgent: urgent,
        Predisposed: predisposed,
        AllInclusive: false,
        UnitCount: 1,
        SplitCount: 2,
        Rounding: 5m,
        InstallationDate: null,
        ServiceItems: [new RepairServiceItemInput(_labour.Id, 2m)]);

    [Fact]
    public async Task Repair_keeps_requested_flags_and_snapshots_service_prices()
    {
        var created = await _sut.CreateAsync(Request());

        Assert.True(created.Urgent);
        Assert.False(created.Predisposed);
        Assert.Equal(RepairStatusCodes.New, created.Status);
        var line = Assert.Single(created.ServiceItems);
        Assert.Equal(45m, line.UnitPrice);
        Assert.Equal(2m * 45m + 5m, created.Total);
    }

    [Fact]
    public async Task Revision_forces_not_urgent_and_predisposed()
    {
        var created = await _sut.CreateAsync(Request(RepairType.Revision, urgent: true, predisposed: false));

        Assert.False(created.Urgent);
        Assert.True(created.Predisposed);
    }

    [Fact]
    public async Task Update_replaces_service_lines_rather_than_appending()
    {
        var created = await _sut.CreateAsync(Request());

        var updated = await _sut.UpdateAsync(created.Id, Request() with
        {
            ServiceItems = [new RepairServiceItemInput(_labour.Id, 1m)]
        });

        Assert.Equal(1m, Assert.Single(updated.ServiceItems).Quantity);
    }

    [Fact]
    public async Task Status_change_updates_status_and_appends_stamped_history()
    {
        var created = await _sut.CreateAsync(Request());

        await _sut.ChangeStatusAsync(created.Id, new ChangeRepairStatusRequest(RepairStatusCodes.Scheduled));
        await _sut.ChangeStatusAsync(created.Id, new ChangeRepairStatusRequest(RepairStatusCodes.InProgress));

        var detail = await _sut.GetByIdAsync(created.Id);
        Assert.Equal(RepairStatusCodes.InProgress, detail.Status);
        Assert.Equal([RepairStatusCodes.Scheduled, RepairStatusCodes.InProgress], detail.StatusEvents.Select(e => e.Status));
        Assert.All(detail.StatusEvents, e => Assert.Equal("tech@example.com", e.ChangedBy));
    }

    [Fact]
    public async Task Schedule_entry_resolves_the_installer_name()
    {
        var installer = new Installer { Name = "Luigi" };
        _db.Installers.Add(installer);
        _db.SaveChanges();
        var created = await _sut.CreateAsync(Request());

        var entry = await _sut.AddScheduleEntryAsync(created.Id, new AddRepairScheduleEntryRequest(installer.Id, DateTime.UtcNow.AddDays(1), "AM"));

        Assert.Equal("Luigi", entry.InstallerName);
    }

    [Fact]
    public async Task Unknown_client_or_service_item_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreateAsync(Request() with { ClientId = 999 }));
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreateAsync(Request() with
        {
            ServiceItems = [new RepairServiceItemInput(999, 1m)]
        }));
    }
}
