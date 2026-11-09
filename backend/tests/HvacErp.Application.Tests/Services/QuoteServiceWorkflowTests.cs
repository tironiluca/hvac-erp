using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Pricing;
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
    private readonly Dictionary<string, IncentiveScheme> _schemes;

    public QuoteServiceWorkflowTests()
    {
        TestDb.SeedVatOptions(_db);
        TestDb.SeedBaseInstallationItems(_db);
        var list = TestDb.AddPriceList(_db);
        _external = TestDb.AddProduct(_db, list, 1000m);
        _schemes = TestDb.SeedIncentiveSchemes(_db);
        _client = TestDb.AddClient(_db);
        _db.SaveChanges();

        _sut = new QuoteService(_db, new QuoteNumberingService(_db), new QuotePricingService(_db), new FakeCurrentUser("sales@example.com"));
    }

    private int SchemeId(string code) => _schemes[code].Id;

    private UpsertQuoteRequest Request(params QuoteIncentiveInput[] incentives) => LineRequest([], incentives);

    private UpsertQuoteRequest LineRequest(List<QuoteProductIncentiveInput> lineIncentives, params QuoteIncentiveInput[] incentives) => new(
        ClientId: _client.Id,
        OperatorId: null,
        ParentQuoteId: null,
        Department: QuoteDepartmentCodes.Store,
        Predisposed: true,
        Vat: VatCodes.Exempt,
        ManualDiscount: 0m,
        InstallationAddress: "  Via Roma 1  ",
        InstallationCity: " ",
        InstallationPositionNotes: null,
        Products: [new QuoteProductInput(_external.Id, ProductRole.ExternalUnit, 1, lineIncentives)],
        DuctedUnits: [],
        ServiceItems: [],
        Extras: [],
        Incentives: [.. incentives]);

    private QuoteIncentiveInput Discount => new(SchemeId(IncentiveSchemeDefaults.InvoiceDiscount50));

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
    public async Task Incentive_confirmation_toggles_only_for_schemes_that_need_it()
    {
        var discountId = SchemeId(IncentiveSchemeDefaults.InvoiceDiscount50);
        var weeeId = SchemeId(IncentiveSchemeDefaults.WeeeTakeBack);
        var withoutDiscount = await _sut.CreateAsync(Request(new QuoteIncentiveInput(weeeId, 30m)));
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.ToggleIncentiveConfirmationAsync(withoutDiscount.Id, discountId));
        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.ToggleIncentiveConfirmationAsync(withoutDiscount.Id, weeeId));

        var withDiscount = await _sut.CreateAsync(Request(Discount));
        await _sut.ConfirmAsync(withDiscount.Id);
        await _sut.ToggleIncentiveConfirmationAsync(withDiscount.Id, discountId); // still allowed once locked

        Assert.True((await _sut.GetByIdAsync(withDiscount.Id)).Incentives.Single().Confirmed);
    }

    [Fact]
    public async Task Saving_a_quote_keeps_the_customer_acceptance_and_applies_scheme_defaults()
    {
        var deductionId = SchemeId(IncentiveSchemeDefaults.EnergyTaxDeduction);
        _schemes[IncentiveSchemeDefaults.EnergyTaxDeduction].DefaultAmount = 120m;
        await _db.SaveChangesAsync();
        var created = await _sut.CreateAsync(Request(Discount));
        await _sut.ToggleIncentiveConfirmationAsync(created.Id, Discount.IncentiveSchemeId);

        var updated = await _sut.UpdateAsync(created.Id, Request(Discount, new QuoteIncentiveInput(deductionId)));

        Assert.True(updated.Incentives.Single(i => i.IncentiveSchemeId == Discount.IncentiveSchemeId).Confirmed);
        Assert.Equal(120m, updated.Incentives.Single(i => i.IncentiveSchemeId == deductionId).Amount);
    }

    [Fact]
    public async Task Line_schemes_go_on_lines_and_quote_schemes_on_the_quote()
    {
        var warrantyId = SchemeId(IncentiveSchemeDefaults.AllInclusiveWarranty);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateAsync(Request(new QuoteIncentiveInput(warrantyId))));
        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateAsync(LineRequest([new(Discount.IncentiveSchemeId)])));

        var created = await _sut.CreateAsync(LineRequest([new(warrantyId)]));
        var line = created.Products.Single().Incentives.Single();
        Assert.Equal((LineAmountModes.Percent, 8m, 80m), (line.Mode, line.Value, line.Amount)); // scheme default: 8% of the 1000 list price
        Assert.Equal(1080m, created.FinalPrice); // warranty on the machines side; Exempt VAT, no indoor unit
    }

    [Fact]
    public async Task Staff_choose_a_percentage_or_a_fixed_amount_per_unit_on_each_line()
    {
        var warrantyId = SchemeId(IncentiveSchemeDefaults.AllInclusiveWarranty);
        var request = LineRequest([new(warrantyId, LineAmountModes.Fixed, 100m)]) with
        {
            Products = [new QuoteProductInput(_external.Id, ProductRole.ExternalUnit, 2, [new(warrantyId, LineAmountModes.Fixed, 100m)])],
        };

        var fixedPerUnit = await _sut.CreateAsync(request);
        var percent = await _sut.CreateAsync(LineRequest([new(warrantyId, LineAmountModes.Percent, 12.5m)]));

        Assert.Equal(200m, fixedPerUnit.Products.Single().Incentives.Single().Amount); // €100 × 2 units
        Assert.Equal(2000m + 200m, fixedPerUnit.FinalPrice);
        Assert.Equal(125m, percent.Products.Single().Incentives.Single().Amount); // 12.5% of 1000
    }

    [Fact]
    public async Task A_line_mode_without_a_value_takes_the_scheme_default_for_that_mode_and_bad_input_is_rejected()
    {
        var warranty = _schemes[IncentiveSchemeDefaults.AllInclusiveWarranty];
        warranty.DefaultAmount = 150m;
        await _db.SaveChangesAsync();

        var created = await _sut.CreateAsync(LineRequest([new(warranty.Id, LineAmountModes.Fixed)]));

        Assert.Equal((LineAmountModes.Fixed, 150m, 150m),
            (created.Products.Single().Incentives.Single().Mode, created.Products.Single().Incentives.Single().Value, created.Products.Single().Incentives.Single().Amount));
        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateAsync(LineRequest([new(warranty.Id, "PerQuote", 10m)])));
        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateAsync(LineRequest([new(warranty.Id, LineAmountModes.Fixed, -1m)])));
    }

    [Fact]
    public async Task Only_one_invoice_discount_and_no_newly_added_inactive_scheme()
    {
        var second = new IncentiveScheme
        {
            Code = "INVOICE_DISCOUNT_65", Label = "65%", Kind = IncentiveKinds.InvoiceDiscount, Threshold = 1000m,
            RateBelowThreshold = 1.2m, RateAboveThreshold = 1.1m, RoundingStep = 10m, Percent = 35m,
        };
        var retired = new IncentiveScheme { Code = "OLD_FEE", Label = "Old fee", Kind = IncentiveKinds.QuoteAmount, Active = false };
        _db.IncentiveSchemes.AddRange(second, retired);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateAsync(Request(Discount, new QuoteIncentiveInput(second.Id))));
        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateAsync(Request(new QuoteIncentiveInput(retired.Id))));
    }

    [Fact]
    public async Task Warehouse_users_cannot_confirm_or_toggle_an_incentive()
    {
        var created = await _sut.CreateAsync(Request(Discount));
        var asWarehouse = new QuoteService(_db, new QuoteNumberingService(_db), new QuotePricingService(_db),
            new FakeCurrentUser("stock@example.com", "user-2", UserRole.Warehouse));

        await Assert.ThrowsAsync<ForbiddenException>(() => asWarehouse.ConfirmAsync(created.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => asWarehouse.ToggleIncentiveConfirmationAsync(created.Id, Discount.IncentiveSchemeId));
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
