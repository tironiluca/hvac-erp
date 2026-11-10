using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Erp;
using HvacErp.Application.Pricing;
using HvacErp.Application.Services;
using HvacErp.Application.Tests.Support;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;
using HvacErp.Infrastructure.Persistence;

namespace HvacErp.Application.Tests.Erp;

public class ErpExportServiceTests
{
    private readonly AppDbContext _db = TestDb.Create();
    private readonly FakeConnector _connector = new();
    private readonly FakeResolver _resolver;
    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.Zero));
    private readonly ErpExportService _erp;
    private readonly QuoteService _quotes;
    private readonly Client _client;
    private readonly Product _external;
    private readonly Dictionary<string, IncentiveScheme> _schemes;

    public ErpExportServiceTests()
    {
        TestDb.SeedVatOptions(_db);
        TestDb.SeedBaseInstallationItems(_db);
        var list = TestDb.AddPriceList(_db);
        _external = TestDb.AddProduct(_db, list, 1000m);
        _client = TestDb.AddClient(_db);
        _schemes = TestDb.SeedIncentiveSchemes(_db);
        _db.SaveChanges();

        _resolver = new FakeResolver(_connector);
        var user = new FakeCurrentUser("sales@example.com");
        _erp = new ErpExportService(_db, _resolver, user, _clock);
        _quotes = new QuoteService(_db, new QuoteNumberingService(_db), new QuotePricingService(_db), user, _erp);
    }

    private async Task<int> CreateQuoteAsync(bool confirm)
    {
        var created = await _quotes.CreateAsync(new UpsertQuoteRequest(
            _client.Id, null, null, QuoteDepartmentCodes.Store, true, VatCodes.Standard22, 0m,
            "Via Roma 1", "Bergamo", "Balcony",
            [new QuoteProductInput(_external.Id, ProductRole.ExternalUnit, 2)], [], [], [],
            Incentives:
            [
                new QuoteIncentiveInput(_schemes[IncentiveSchemeDefaults.AllInclusiveWarranty].Id, AmountModes.Fixed, 100m),
                new QuoteIncentiveInput(_schemes[IncentiveSchemeDefaults.InvoiceDiscount50].Id),
            ]));
        if (confirm)
        {
            await _quotes.ConfirmAsync(created.Id);
        }
        return created.Id;
    }

    [Fact]
    public async Task Confirming_a_quote_queues_its_order_with_every_priced_line()
    {
        var quoteId = await CreateQuoteAsync(confirm: true);

        var export = await _db.ErpOrderExports.SingleAsync();
        Assert.Equal((quoteId, ErpExportStatuses.Pending, "Fake"), (export.QuoteId, export.Status, export.Connector));

        var order = ErpOrder.FromJson(export.PayloadJson);
        Assert.Equal($"quote-{quoteId}", order.ExternalReference);
        Assert.Equal(_client.FullName, order.Customer.Name);
        Assert.NotNull(order.ConfirmedAt);
        Assert.Equal("Via Roma 1", order.InstallationSite!.Street);
        var product = Assert.Single(order.Lines, l => l.Kind == ErpLineKinds.Product);
        Assert.Equal((2m, 2000m), (product.Quantity, product.Amount));
        var warranty = Assert.Single(order.Lines, l => l.Kind == ErpLineKinds.Incentive);
        Assert.Equal((IncentiveSchemeDefaults.AllInclusiveWarranty, 100m), (warranty.Code, warranty.Amount));
        Assert.NotNull(order.Totals.InvoiceDiscountTotal);
        Assert.Equal(order.Totals.InvoiceDiscountTotal / 2, order.Totals.CustomerPays);
    }

    [Fact]
    public async Task Nothing_is_queued_while_the_integration_is_off()
    {
        _resolver.Off = true;

        await CreateQuoteAsync(confirm: true);

        Assert.Empty(_db.ErpOrderExports);
    }

    [Fact]
    public async Task A_due_export_is_sent_and_records_the_erp_order_number()
    {
        await CreateQuoteAsync(confirm: true);

        Assert.Equal(1, await _erp.ProcessDueAsync());

        var export = await _db.ErpOrderExports.SingleAsync();
        Assert.Equal((ErpExportStatuses.Sent, "ERP-1", 1), (export.Status, export.ErpOrderNumber, export.Attempts));
        Assert.Equal(0, await _erp.ProcessDueAsync()); // nothing left to send
    }

    [Fact]
    public async Task A_quote_whose_order_is_in_the_erp_can_no_longer_be_deleted()
    {
        var pending = await CreateQuoteAsync(confirm: true);
        await _quotes.DeleteAsync(pending); // queued but never sent: nothing outside knows about it
        Assert.Empty(_db.ErpOrderExports);

        var sent = await CreateQuoteAsync(confirm: true);
        await _erp.ProcessDueAsync();

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _quotes.DeleteAsync(sent));
        Assert.Contains("ERP", error.Message);
        Assert.Equal(ErpExportStatuses.Sent, (await _db.ErpOrderExports.SingleAsync()).Status);
    }

    [Fact]
    public async Task A_temporary_failure_is_retried_with_back_off_until_it_succeeds()
    {
        var quoteId = await CreateQuoteAsync(confirm: true);
        _connector.Results.Enqueue(ErpPushResult.TryLater("503"));
        _connector.Results.Enqueue(ErpPushResult.TryLater("timeout"));

        await _erp.ProcessDueAsync();
        var export = await _db.ErpOrderExports.SingleAsync();
        Assert.Equal((ErpExportStatuses.Pending, 1, "503"), (export.Status, export.Attempts, export.LastError));
        Assert.Equal(_clock.GetUtcNow().UtcDateTime.AddMinutes(1), export.NextAttemptAt);

        Assert.Equal(0, await _erp.ProcessDueAsync()); // not due yet
        _clock.Advance(TimeSpan.FromMinutes(1));
        await _erp.ProcessDueAsync();
        Assert.Equal(_clock.GetUtcNow().UtcDateTime.AddMinutes(2), export.NextAttemptAt);

        _clock.Advance(TimeSpan.FromMinutes(2));
        await _erp.ProcessDueAsync();
        Assert.Equal((ErpExportStatuses.Sent, 3), (export.Status, export.Attempts));
        Assert.Equal(ErpExportStatuses.Sent, (await _erp.GetAsync(quoteId))!.Status);
    }

    [Fact]
    public async Task A_rejection_fails_at_once_and_retries_stop_after_the_maximum()
    {
        await CreateQuoteAsync(confirm: true);
        _connector.Results.Enqueue(ErpPushResult.Rejected("400 bad customer"));

        await _erp.ProcessDueAsync();
        var export = await _db.ErpOrderExports.SingleAsync();
        Assert.Equal((ErpExportStatuses.Failed, "400 bad customer"), (export.Status, export.LastError));

        export.Status = ErpExportStatuses.Pending;
        export.Attempts = ErpExportService.MaxAttempts - 1;
        await _db.SaveChangesAsync();
        _connector.Results.Enqueue(ErpPushResult.TryLater("still down"));
        await _erp.ProcessDueAsync();
        Assert.Equal(ErpExportStatuses.Failed, export.Status);
    }

    [Fact]
    public async Task A_connector_exception_counts_as_a_temporary_failure()
    {
        await CreateQuoteAsync(confirm: true);
        _connector.Throw = true;

        await _erp.ProcessDueAsync();

        var export = await _db.ErpOrderExports.SingleAsync();
        Assert.Equal((ErpExportStatuses.Pending, "boom"), (export.Status, export.LastError));
    }

    [Fact]
    public async Task Send_now_needs_a_confirmed_quote_retries_a_failed_export_and_covers_older_quotes()
    {
        var draft = await CreateQuoteAsync(confirm: false);
        await Assert.ThrowsAsync<BusinessRuleException>(() => _erp.SendNowAsync(draft));

        // Confirmed while the integration was off: no export yet; a manual send creates and sends it.
        _resolver.Off = true;
        var older = await CreateQuoteAsync(confirm: true);
        _resolver.Off = false;
        Assert.Null(await _erp.GetAsync(older));
        var sent = await _erp.SendNowAsync(older);
        Assert.Equal((ErpExportStatuses.Sent, "ERP-1"), (sent.Status, sent.ErpOrderNumber));
        Assert.Equal(sent, await _erp.SendNowAsync(older)); // already sent: unchanged, no second push

        var failing = await CreateQuoteAsync(confirm: true);
        _connector.Results.Enqueue(ErpPushResult.Rejected("nope"));
        await _erp.ProcessDueAsync();
        var retried = await _erp.SendNowAsync(failing);
        Assert.Equal((ErpExportStatuses.Sent, 1), (retried.Status, retried.Attempts));
    }

    [Fact]
    public async Task Warehouse_users_cannot_send_to_the_erp()
    {
        var quoteId = await CreateQuoteAsync(confirm: true);
        var asWarehouse = new ErpExportService(_db, _resolver, new FakeCurrentUser("stock@example.com", "u2", UserRole.Warehouse), _clock);

        await Assert.ThrowsAsync<ForbiddenException>(() => asWarehouse.SendNowAsync(quoteId));
    }

    [Fact]
    public void Back_off_doubles_from_a_minute_and_caps_at_an_hour()
    {
        Assert.Equal([1, 2, 4, 8, 16, 32, 60, 60], Enumerable.Range(1, 8).Select(a => ErpExportService.BackOff(a).TotalMinutes));
    }

    private sealed class FakeConnector : IErpConnector
    {
        private int _number;
        public Queue<ErpPushResult> Results { get; } = new();
        public bool Throw { get; set; }
        public string Name => "Fake";

        public Task<ErpPushResult> PushOrderAsync(ErpOrder order, CancellationToken cancellationToken = default)
        {
            if (Throw)
            {
                throw new HttpRequestException("boom");
            }
            return Task.FromResult(Results.TryDequeue(out var result) ? result : ErpPushResult.Accepted($"ERP-{++_number}"));
        }
    }

    private sealed class FakeResolver(IErpConnector connector) : IErpConnectorResolver
    {
        public bool Off { get; set; }
        public IErpConnector? Current => Off ? null : connector;
        public IErpConnector? ByName(string name) => name == connector.Name ? connector : null;
    }

    private sealed class ManualClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }
}
