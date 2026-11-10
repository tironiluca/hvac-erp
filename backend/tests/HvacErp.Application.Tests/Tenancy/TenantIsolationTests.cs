using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Erp;
using HvacErp.Application.Services;
using HvacErp.Application.Tests.Support;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;
using HvacErp.Infrastructure.Persistence;

namespace HvacErp.Application.Tests.Tenancy;

/// <summary>
/// One database looked at as different tenants and stores: tenant A (id 1) with stores 1 and 2,
/// tenant B (id 2) with store 3. Every assertion goes through the real services and the real
/// <see cref="AppDbContext"/>, so it is the query filters and the write guard that are under test.
/// </summary>
public class TenantIsolationTests
{
    private const int TenantA = 1;
    private const int TenantB = 2;

    private readonly string _database = $"tenancy-{Guid.NewGuid():N}";
    private readonly AppDbContext _a1;
    private readonly AppDbContext _a2;
    private readonly AppDbContext _b;
    private readonly Client _clientA;
    private readonly Client _clientB;
    private readonly Product _productA;
    private readonly Product _productB;

    public TenantIsolationTests()
    {
        _a1 = Open(FixedTenantContext.For(TenantA, 1));
        _a2 = Open(FixedTenantContext.For(TenantA, 2));
        _b = Open(FixedTenantContext.For(TenantB, 3));

        TestDb.SeedVatOptions(_a1);
        (_clientA, _productA) = SeedCatalogue(_a1, "CA001", "Anna Alfa");
        (_clientB, _productB) = SeedCatalogue(_b, "CB001", "Bruno Beta");
    }

    private AppDbContext Open(ITenantContext tenant) => TestDb.Create(tenant, _database);

    private static (Client, Product) SeedCatalogue(AppDbContext db, string clientCode, string clientName)
    {
        TestDb.SeedBaseInstallationItems(db);
        var product = TestDb.AddProduct(db, TestDb.AddPriceList(db), 1000m);
        var client = TestDb.AddClient(db, clientCode, clientName);
        db.SaveChanges();
        return (client, product);
    }

    private static QuoteService Quotes(AppDbContext db, IErpExportService? erp = null) =>
        new(db, new QuoteNumberingService(db), new QuotePricingService(db), new FakeCurrentUser(), erp);

    private static UpsertQuoteRequest QuoteRequest(Client client, Product product) => new(
        client.Id, null, null, QuoteDepartmentCodes.Store, true, VatCodes.Exempt, 0m, null, null, null,
        [new QuoteProductInput(product.Id, ProductRole.ExternalUnit, 1)], [], [], []);

    [Fact]
    public async Task A_tenant_reads_only_its_own_rows()
    {
        var quoteA = await Quotes(_a1).CreateAsync(QuoteRequest(_clientA, _productA));
        var quoteB = await Quotes(_b).CreateAsync(QuoteRequest(_clientB, _productB));

        Assert.Equal(["Anna Alfa"], (await new ClientService(_a1).SearchAsync()).Select(c => c.FullName));
        Assert.Equal(["Bruno Beta"], (await new ClientService(_b).SearchAsync()).Select(c => c.FullName));
        Assert.Equal([quoteA.Id], (await Quotes(_a1).GetListAsync()).Select(q => q.Id));
        Assert.Equal([quoteB.Id], (await Quotes(_b).GetListAsync()).Select(q => q.Id));
        Assert.Equal([_productA.Id], (await new LookupService(_a1).GetProductsAsync()).Select(p => p.Id));
    }

    [Fact]
    public async Task Another_tenants_ids_are_not_found()
    {
        var quoteA = await Quotes(_a1).CreateAsync(QuoteRequest(_clientA, _productA));

        await Assert.ThrowsAsync<NotFoundException>(() => new ClientService(_b).GetByIdAsync(_clientA.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => Quotes(_b).GetByIdAsync(quoteA.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => Quotes(_b).ConfirmAsync(quoteA.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => Quotes(_b).DeleteAsync(quoteA.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => new ClientService(_b).DeleteAsync(_clientA.Id));
    }

    [Fact]
    public async Task A_record_cannot_reference_another_tenants_rows()
    {
        // Tenant B's caller sends tenant A's client and product ids.
        await Assert.ThrowsAsync<NotFoundException>(() => Quotes(_b).CreateAsync(QuoteRequest(_clientA, _productB)));
        await Assert.ThrowsAsync<NotFoundException>(() => Quotes(_b).CreateAsync(QuoteRequest(_clientB, _productA)));
        await Assert.ThrowsAsync<NotFoundException>(() => new RepairService(_b, new FakeCurrentUser()).CreateAsync(
            new UpsertRepairRequest(_clientA.Id, null, RepairType.Repair, null, "x", false, false, false, 1, 1, 0m, null, [])));
    }

    [Fact]
    public async Task New_rows_are_stamped_with_the_callers_tenant_and_store()
    {
        var quote = await Quotes(_a2).CreateAsync(QuoteRequest(_clientA, _productA));

        using var system = _a1.AsSystem();
        var stored = await _a1.Quotes.Include(q => q.Products).Include(q => q.StatusEvents).SingleAsync(q => q.Id == quote.Id);
        Assert.Equal((TenantA, 2), (stored.TenantId, stored.StoreId));
        Assert.All(stored.Products, p => Assert.Equal(TenantA, p.TenantId));
        Assert.Equal(TenantA, _clientA.TenantId);
        Assert.Equal(TenantB, _clientB.TenantId);
    }

    [Fact]
    public async Task A_store_reads_only_its_own_records_but_shares_the_tenants_clients()
    {
        var inStore1 = await Quotes(_a1).CreateAsync(QuoteRequest(_clientA, _productA));
        var inStore2 = await Quotes(_a2).CreateAsync(QuoteRequest(_clientA, _productA));

        Assert.Equal([inStore1.Id], (await Quotes(_a1).GetListAsync()).Select(q => q.Id));
        Assert.Equal([inStore2.Id], (await Quotes(_a2).GetListAsync()).Select(q => q.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => Quotes(_a2).GetByIdAsync(inStore1.Id));
        Assert.Single(await new ClientService(_a2).SearchAsync());

        // Someone who works in both stores sees both.
        var both = Open(FixedTenantContext.For(TenantA, [1, 2]));
        Assert.Equal(2, (await Quotes(both).GetListAsync()).Count);
    }

    [Fact]
    public async Task Lines_of_another_stores_record_are_out_of_reach()
    {
        var quote = await Quotes(_a1).CreateAsync(QuoteRequest(_clientA, _productA));
        _a1.QuoteAttachments.Add(new QuoteAttachment { QuoteId = quote.Id, FileName = "a.jpg", FilePath = "quotes/a.jpg" });
        await _a1.SaveChangesAsync();
        var attachmentId = _a1.QuoteAttachments.Single().Id;

        await Assert.ThrowsAsync<NotFoundException>(
            () => new QuoteAttachmentService(_a2, new FakeStorage()).DeleteAsync(quote.Id, attachmentId));
        await Assert.ThrowsAsync<NotFoundException>(
            () => new QuoteAttachmentService(_a2, new FakeStorage()).GetUrlAsync(quote.Id, attachmentId));
        await Assert.ThrowsAsync<NotFoundException>(() => Quotes(_a2).ToggleIncentiveConfirmationAsync(quote.Id, 1));
        await Assert.ThrowsAsync<NotFoundException>(
            () => new FGasRecordService(_a2).CreateAsync(new UpsertFGasRecordRequest(quote.Id, null, "R32", 1m, false, false)));

        await new QuoteAttachmentService(_a1, new FakeStorage()).DeleteAsync(quote.Id, attachmentId);
    }

    [Fact]
    public async Task Quote_numbers_run_per_store()
    {
        var first = await Quotes(_a1).CreateAsync(QuoteRequest(_clientA, _productA));
        var otherStore = await Quotes(_a2).CreateAsync(QuoteRequest(_clientA, _productA));
        var otherTenant = await Quotes(_b).CreateAsync(QuoteRequest(_clientB, _productB));

        Assert.Equal((1, 1, 0), (first.Number, first.Version, first.Revision));
        Assert.Equal((1, 1, 0), (otherStore.Number, otherStore.Version, otherStore.Revision));
        Assert.Equal((1, 1, 0), (otherTenant.Number, otherTenant.Version, otherTenant.Revision));
    }

    [Fact]
    public async Task Working_in_several_stores_requires_choosing_one_to_create_in()
    {
        var both = Open(FixedTenantContext.For(TenantA, [1, 2]));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Quotes(both).CreateAsync(QuoteRequest(_clientA, _productA)));
        Assert.Contains("store", error.Message);

        var chosen = Open(FixedTenantContext.For(TenantA, [1, 2], activeStoreId: 2));
        var quote = await Quotes(chosen).CreateAsync(QuoteRequest(_clientA, _productA));
        Assert.Equal([quote.Id], (await Quotes(_a2).GetListAsync()).Select(q => q.Id));
    }

    [Fact]
    public async Task Follow_up_records_live_in_their_quotes_store_not_the_callers()
    {
        var quote = await Quotes(_a2).CreateAsync(QuoteRequest(_clientA, _productA));
        // Works in both stores, creating in store 1 — the quote is in store 2.
        var both = Open(FixedTenantContext.For(TenantA, [1, 2], activeStoreId: 1));
        both.DaySlots.Add(new DaySlot { Code = DaySlotCodes.Morning, Label = "Morning" });
        var erp = new ErpExportService(both, new SingleConnector(), new FakeCurrentUser(), TimeProvider.System);
        await Quotes(both, erp).ConfirmAsync(quote.Id);

        var installation = await new InstallationService(both, new FakeCurrentUser(), new RecordingCalendarNotifier())
            .CreateAsync(new UpsertScheduledInstallationRequest(quote.Id, DateTime.UtcNow.AddDays(3), DaySlotCodes.Morning, null, []));
        var gas = await new FGasRecordService(both).CreateAsync(new UpsertFGasRecordRequest(quote.Id, null, "R32", 1.2m, false, false));

        Assert.Equal(2, installation.StoreId);
        using var system = _a1.AsSystem();
        Assert.Equal(2, (await _a1.FGasRecords.SingleAsync(f => f.Id == gas.Id)).StoreId);
        Assert.Equal((TenantA, 2), await _a1.ErpOrderExports.Select(e => new ValueTuple<int, int>(e.TenantId, e.StoreId)).SingleAsync());
    }

    [Fact]
    public async Task Writing_into_another_tenant_or_store_is_refused()
    {
        _b.Clients.Add(new Client { TenantId = TenantA, Code = "X", FullName = "Intruder" });
        await Assert.ThrowsAsync<ForbiddenException>(() => _b.SaveChangesAsync());

        var wrongStore = Open(FixedTenantContext.For(TenantA, 1));
        wrongStore.Repairs.Add(new Repair { StoreId = 2, ClientId = _clientA.Id, Description = "x" });
        await Assert.ThrowsAsync<ForbiddenException>(() => wrongStore.SaveChangesAsync());

        // An existing row cannot be moved to another tenant either.
        var mover = Open(FixedTenantContext.For(TenantA, 1));
        var client = await mover.Clients.SingleAsync();
        client.TenantId = TenantB;
        await Assert.ThrowsAsync<ForbiddenException>(() => mover.SaveChangesAsync());
    }

    [Fact]
    public async Task Without_a_tenant_nothing_is_read_and_nothing_is_written()
    {
        var nobody = Open(FixedTenantContext.None);

        Assert.Empty(await nobody.Clients.ToListAsync());
        Assert.Empty(await nobody.Products.ToListAsync());
        Assert.Null(await nobody.Clients.FindAsync(_clientA.Id));
        Assert.NotEmpty(await nobody.VatOptions.ToListAsync()); // shared lookups are not tenant-owned

        nobody.Clients.Add(new Client { Code = "X", FullName = "Nobody's" });
        await Assert.ThrowsAsync<ForbiddenException>(() => nobody.SaveChangesAsync());
    }

    [Fact]
    public async Task The_system_scope_sees_every_tenant_and_must_name_the_owner_of_what_it_writes()
    {
        var worker = Open(FixedTenantContext.None);
        using (worker.AsSystem())
        {
            Assert.Equal(2, await worker.Clients.CountAsync());

            worker.Clients.Add(new Client { Code = "X", FullName = "Ownerless" });
            await Assert.ThrowsAsync<InvalidOperationException>(() => worker.SaveChangesAsync());
        }

        // Leaving the scope puts the filters back.
        Assert.Empty(await worker.Clients.ToListAsync());
    }

    private sealed class SingleConnector : IErpConnectorResolver, IErpConnector
    {
        public string Name => "Test";
        public IErpConnector? Current => this;
        public IErpConnector? ByName(string name) => this;

        public Task<ErpPushResult> PushOrderAsync(ErpOrder order, CancellationToken cancellationToken = default) =>
            Task.FromResult(ErpPushResult.TryLater("not in this test"));
    }
}
