using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Services;
using HvacErp.Application.Tests.Support;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;
using HvacErp.Infrastructure.Persistence;

namespace HvacErp.Application.Tests.Services;

public class QuoteServiceWorkflowTests
{
    private readonly AppDbContext _db = TestDb.Create();
    private readonly QuoteService _sut;
    private readonly Client _client;
    private readonly Product _external;

    public QuoteServiceWorkflowTests()
    {
        TestDb.SeedVatOptions(_db);
        TestDb.SeedBaseInstallationItems(_db);
        var list = TestDb.AddPriceList(_db);
        _external = TestDb.AddProduct(_db, list, 1000m);
        _client = TestDb.AddClient(_db);
        _db.SaveChanges();

        _sut = new QuoteService(_db, new QuoteNumberingService(_db), new QuotePricingService(_db), new FakeCurrentUser("sales@example.com"));
    }

    private UpsertQuoteRequest Request(bool sfRequested = false) => new(
        ClientId: _client.Id,
        OperatorId: null,
        ParentQuoteId: null,
        Department: QuoteDepartmentCodes.Store,
        Predisposed: true,
        Vat: VatCodes.Exempt,
        ManualDiscount: 0m,
        EneaEligible: false,
        EneaPrice: null,
        RaeeEligible: false,
        RaeePrice: null,
        SfRequested: sfRequested,
        SfConfirmed: false,
        InstallationAddress: "  Via Roma 1  ",
        InstallationCity: " ",
        InstallationPositionNotes: null,
        Products: [new QuoteProductInput(_external.Id, ProductRole.ExternalUnit, 1, false)],
        DuctedUnits: [],
        ServiceItems: [],
        Extras: []);

    [Fact]
    public async Task Create_numbers_prices_and_normalises_the_quote()
    {
        var created = await _sut.CreateAsync(Request());

        Assert.Equal((1, 1, 0), (created.Number, created.Version, created.Revision));
        Assert.Equal(1000m, created.FinalPrice);
        Assert.Equal(QuoteStatusCodes.Draft, created.CurrentStatus);
        Assert.Equal("Via Roma 1", created.InstallationAddress);
        Assert.Null(created.InstallationCity);
    }

    [Fact]
    public async Task Create_for_unknown_client_is_not_found()
    {
        var request = Request() with { ClientId = 999 };

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreateAsync(request));
    }

    [Fact]
    public async Task Status_transitions_append_events_stamped_with_the_caller()
    {
        var created = await _sut.CreateAsync(Request());

        await _sut.MarkSurveyDoneAsync(created.Id);
        await _sut.RecallAsync(created.Id, new RecallQuoteRequest("Call back Monday"));

        var detail = await _sut.GetByIdAsync(created.Id);
        Assert.Equal(QuoteStatusCodes.Recalled, detail.CurrentStatus);
        Assert.Collection(detail.StatusEvents.OrderBy(e => e.Id),
            e => Assert.Equal(QuoteStatusCodes.SurveyDone, e.Status),
            e =>
            {
                Assert.Equal(QuoteStatusCodes.Recalled, e.Status);
                Assert.Equal("Call back Monday", e.Notes);
            });
        Assert.All(detail.StatusEvents, e => Assert.Equal("sales@example.com", e.ChangedBy));
    }

    [Fact]
    public async Task Confirmed_is_terminal_and_never_regresses()
    {
        var created = await _sut.CreateAsync(Request());

        await _sut.ConfirmAsync(created.Id);
        await _sut.RecallAsync(created.Id, new RecallQuoteRequest(null));

        var detail = await _sut.GetByIdAsync(created.Id);
        Assert.Equal(QuoteStatusCodes.Confirmed, detail.CurrentStatus);
        Assert.Equal(2, detail.StatusEvents.Count); // history still records the recall
    }

    [Fact]
    public async Task A_confirmed_quote_cannot_be_edited()
    {
        var created = await _sut.CreateAsync(Request());
        await _sut.ConfirmAsync(created.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.UpdateAsync(created.Id, Request()));
    }

    [Fact]
    public async Task Sf_confirmation_toggles_only_when_requested()
    {
        var withoutSf = await _sut.CreateAsync(Request());
        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.ToggleSfConfirmationAsync(withoutSf.Id));

        var withSf = await _sut.CreateAsync(Request(sfRequested: true));
        await _sut.ConfirmAsync(withSf.Id);
        await _sut.ToggleSfConfirmationAsync(withSf.Id); // still allowed once locked

        Assert.True((await _sut.GetByIdAsync(withSf.Id)).SfConfirmed);
    }

    [Fact]
    public async Task Warehouse_users_cannot_confirm_or_toggle_sf()
    {
        var created = await _sut.CreateAsync(Request(sfRequested: true));
        var asWarehouse = new QuoteService(_db, new QuoteNumberingService(_db), new QuotePricingService(_db),
            new FakeCurrentUser("stock@example.com", "user-2", UserRole.Warehouse));

        await Assert.ThrowsAsync<ForbiddenException>(() => asWarehouse.ConfirmAsync(created.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => asWarehouse.ToggleSfConfirmationAsync(created.Id));
        Assert.Equal(QuoteStatusCodes.Draft, (await _sut.GetByIdAsync(created.Id)).CurrentStatus);
    }

    [Theory]
    [InlineData(UserRole.Sales)]
    [InlineData(UserRole.Admin)]
    public async Task Sales_and_admin_can_confirm(UserRole role)
    {
        var created = await _sut.CreateAsync(Request());
        var sut = new QuoteService(_db, new QuoteNumberingService(_db), new QuotePricingService(_db), new FakeCurrentUser(role: role));

        await sut.ConfirmAsync(created.Id);

        Assert.Equal(QuoteStatusCodes.Confirmed, (await _sut.GetByIdAsync(created.Id)).CurrentStatus);
    }

    [Fact]
    public void System_context_without_a_caller_bypasses_role_checks()
    {
        new FakeCurrentUser(name: null, id: null, role: null).EnsureInRole("seed data", UserRole.Admin);
        Assert.Throws<ForbiddenException>(() => new FakeCurrentUser(role: null).EnsureInRole("do it", UserRole.Admin));
    }

    [Fact]
    public async Task Workflow_calls_on_a_missing_quote_are_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.ConfirmAsync(42));
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.MarkSurveyDoneAsync(42));
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.DeleteAsync(42));
    }
}
