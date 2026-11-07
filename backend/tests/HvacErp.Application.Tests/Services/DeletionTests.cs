using HvacErp.Application.Common;
using HvacErp.Application.Services;
using HvacErp.Application.Tests.Support;
using HvacErp.Domain.Entities;
using HvacErp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HvacErp.Application.Tests.Services;

public class DeletionTests
{
    private readonly AppDbContext _db = TestDb.Create();

    [Fact]
    public async Task Client_without_history_is_deleted_and_referrals_are_cleared()
    {
        var referrer = TestDb.AddClient(_db, "CP0001", "Referrer");
        _db.SaveChanges();
        var friend = TestDb.AddClient(_db, "CP0002", "Friend");
        friend.ReferredByClientId = referrer.Id;
        _db.SaveChanges();

        await new ClientService(_db).DeleteAsync(referrer.Id);

        Assert.False(await _db.Clients.AnyAsync(c => c.Id == referrer.Id));
        Assert.Null((await _db.Clients.SingleAsync()).ReferredByClientId);
    }

    [Fact]
    public async Task Client_with_quotes_or_repairs_is_not_deleted()
    {
        var withQuote = TestDb.AddClient(_db, "CP0001", "Has quote");
        var withRepair = TestDb.AddClient(_db, "CP0002", "Has repair");
        _db.SaveChanges();
        _db.Quotes.Add(new Quote { ClientId = withQuote.Id, Number = 1 });
        _db.Repairs.Add(new Repair { ClientId = withRepair.Id, Description = "x" });
        _db.SaveChanges();
        var sut = new ClientService(_db);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => sut.DeleteAsync(withQuote.Id));
        await Assert.ThrowsAsync<BusinessRuleException>(() => sut.DeleteAsync(withRepair.Id));

        Assert.Contains("1 quote(s)", ex.Message);
        Assert.Equal(2, await _db.Clients.CountAsync());
    }

    [Fact]
    public async Task Deleting_missing_records_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => new ClientService(_db).DeleteAsync(404));
        await Assert.ThrowsAsync<NotFoundException>(() => new RepairService(_db, new FakeCurrentUser()).DeleteAsync(404));
    }

    [Fact]
    public async Task Repair_is_deleted()
    {
        var client = TestDb.AddClient(_db);
        _db.SaveChanges();
        var repair = new Repair { ClientId = client.Id, Description = "Leak" };
        _db.Repairs.Add(repair);
        _db.SaveChanges();

        await new RepairService(_db, new FakeCurrentUser()).DeleteAsync(repair.Id);

        Assert.Empty(_db.Repairs);
        Assert.Single(_db.Clients); // the client stays
    }
}
