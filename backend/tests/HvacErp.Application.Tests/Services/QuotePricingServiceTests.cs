using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Pricing;
using HvacErp.Application.Services;
using HvacErp.Application.Tests.Support;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;
using HvacErp.Infrastructure.Persistence;

namespace HvacErp.Application.Tests.Services;

public class QuotePricingServiceTests
{
    private readonly AppDbContext _db = TestDb.Create();
    private readonly QuotePricingService _sut;
    private readonly ProductPriceList _plainList;
    private readonly Dictionary<string, IncentiveScheme> _schemes;

    public QuotePricingServiceTests()
    {
        TestDb.SeedVatOptions(_db);
        TestDb.SeedBaseInstallationItems(_db);
        _plainList = TestDb.AddPriceList(_db);
        _schemes = TestDb.SeedIncentiveSchemes(_db);
        _db.SaveChanges();
        _sut = new QuotePricingService(_db);
    }

    private Product External(decimal price = 1000m, string gas = "R32", decimal? maxBtu = 10_000m, ProductPriceList? list = null, string brand = "Daikin")
    {
        var p = TestDb.AddProduct(_db, list ?? _plainList, price, brand: brand, gas: gas, maxBtu: maxBtu);
        _db.SaveChanges();
        return p;
    }

    private Product Internal(decimal price = 300m, string gas = "R32", decimal power = 2500m, ProductPriceList? list = null)
    {
        var p = TestDb.AddProduct(_db, list ?? _plainList, price, gas: gas, power: power);
        _db.SaveChanges();
        return p;
    }

    private static Quote QuoteWith(string vat, params (Product Product, ProductRole Role, int Quantity)[] lines)
    {
        var quote = new Quote { Vat = vat, Predisposed = true };
        foreach (var (product, role, quantity) in lines)
        {
            quote.Products.Add(new QuoteProduct { ProductId = product.Id, Role = role, Quantity = quantity });
        }
        return quote;
    }

    private QuoteIncentive AddIncentive(Quote quote, string code, decimal? amount = null)
    {
        var incentive = new QuoteIncentive { IncentiveSchemeId = _schemes[code].Id, Amount = amount };
        quote.Incentives.Add(incentive);
        return incentive;
    }

    private IncentiveScheme AddScheme(IncentiveScheme scheme)
    {
        _db.IncentiveSchemes.Add(scheme);
        _db.SaveChanges();
        return scheme;
    }

    [Fact]
    public async Task Exempt_vat_passes_machines_plus_installation_through_untouched()
    {
        var quote = QuoteWith(VatCodes.Exempt,
            (External(1000m), ProductRole.ExternalUnit, 1),
            (Internal(300m), ProductRole.InternalUnit, 1));

        await _sut.PriceAsync(quote);

        Assert.Equal(1300m, quote.MachinesPrice);
        Assert.Equal(400m, quote.InstallationPrice); // "Mono" base system price
        Assert.Equal(1700m, quote.FinalPrice);
    }

    [Fact]
    public async Task Split_vat_grosses_installation_and_machine_remainder_separately()
    {
        var quote = QuoteWith(VatCodes.Standard22,
            (External(1000m), ProductRole.ExternalUnit, 1),
            (Internal(300m), ProductRole.InternalUnit, 1));

        await _sut.PriceAsync(quote);

        // 400 × 2.2 + (1300 − 400) × 1.22 = 880 + 1098
        Assert.Equal(1978m, quote.FinalPrice);
    }

    [Fact]
    public async Task Flat_vat_applies_one_multiplier_to_the_whole_total()
    {
        var quote = QuoteWith(VatCodes.Reduced10,
            (External(1000m), ProductRole.ExternalUnit, 1),
            (Internal(300m), ProductRole.InternalUnit, 1));

        await _sut.PriceAsync(quote);

        Assert.Equal(1870m, quote.FinalPrice); // (1300 + 400) × 1.1
    }

    [Theory]
    [InlineData(1, 400)]
    [InlineData(2, 600)]
    [InlineData(3, 800)]
    [InlineData(4, 1000)]
    [InlineData(5, 1200)]
    [InlineData(7, 1200)]
    public async Task Base_installation_price_follows_internal_unit_count(int internalUnits, decimal expected)
    {
        var lines = new List<(Product, ProductRole, int)> { (External(1000m, maxBtu: null), ProductRole.ExternalUnit, 1) };
        for (var i = 0; i < internalUnits; i++)
        {
            lines.Add((Internal(100m), ProductRole.InternalUnit, 1));
        }
        var quote = QuoteWith(VatCodes.Exempt, lines.ToArray());

        await _sut.PriceAsync(quote);

        Assert.Equal(expected, quote.InstallationPrice);
    }

    [Fact]
    public async Task No_internal_units_means_no_base_installation_price()
    {
        var quote = QuoteWith(VatCodes.Exempt, (External(1000m), ProductRole.ExternalUnit, 1));

        await _sut.PriceAsync(quote);

        Assert.Equal(0m, quote.InstallationPrice);
        Assert.Equal(1000m, quote.FinalPrice);
    }

    [Fact]
    public async Task Discount_tiers_cascade_on_the_running_balance_in_tier_order()
    {
        var tiered = TestDb.AddPriceList(_db, 10m, 5m);
        _db.SaveChanges();
        var external = External(1000m, list: tiered);
        var quote = QuoteWith(VatCodes.Exempt, (external, ProductRole.ExternalUnit, 1));

        await _sut.PriceAsync(quote);

        var line = Assert.Single(quote.Products);
        Assert.Equal(1000m, line.Price);
        Assert.Equal(855m, line.DiscountedPrice); // 1000 × 0.90 × 0.95, not 1000 × 0.85
        Assert.Equal(855m, quote.MachinesPrice);
    }

    [Fact]
    public async Task Predisposed_flag_selects_prepared_or_not_prepared_price()
    {
        var product = TestDb.AddProduct(_db, _plainList, 999m, pricePrepared: 800m, priceNotPrepared: 900m);
        _db.SaveChanges();

        var prepared = QuoteWith(VatCodes.Exempt, (product, ProductRole.ExternalUnit, 1));
        prepared.Predisposed = true;
        await _sut.PriceAsync(prepared);

        var notPrepared = QuoteWith(VatCodes.Exempt, (product, ProductRole.ExternalUnit, 1));
        notPrepared.Predisposed = false;
        await _sut.PriceAsync(notPrepared);

        Assert.Equal(800m, prepared.MachinesPrice);
        Assert.Equal(900m, notPrepared.MachinesPrice);
    }

    [Fact]
    public async Task Missing_variant_price_falls_back_to_list_price()
    {
        var quote = QuoteWith(VatCodes.Exempt, (External(1234m), ProductRole.ExternalUnit, 1));
        quote.Predisposed = false;

        await _sut.PriceAsync(quote);

        Assert.Equal(1234m, quote.MachinesPrice);
    }

    [Fact]
    public async Task Hydronic_products_use_hydronic_price_and_skip_the_gas_check()
    {
        var hydronic = TestDb.AddProduct(_db, _plainList, 5000m, brand: "Acme Hydronic", gas: "Water", hydronicPrice: 4000m, isHydronic: true);
        _db.SaveChanges();
        var quote = QuoteWith(VatCodes.Exempt,
            (hydronic, ProductRole.ExternalUnit, 1),
            (Internal(300m, gas: "R410A"), ProductRole.InternalUnit, 1));

        await _sut.PriceAsync(quote);

        Assert.Equal(4300m, quote.MachinesPrice);
    }

    [Fact]
    public async Task Quantity_multiplies_net_price_and_a_percentage_line_scheme_is_added_per_unit()
    {
        var quote = QuoteWith(VatCodes.Exempt,
            (External(1000m), ProductRole.ExternalUnit, 1),
            (Internal(300m), ProductRole.InternalUnit, 2));
        var internalLine = quote.Products.Single(p => p.Role == ProductRole.InternalUnit);
        var warranty = new QuoteProductIncentive
        {
            IncentiveSchemeId = _schemes[IncentiveSchemeDefaults.AllInclusiveWarranty].Id, Mode = LineAmountModes.Percent, Value = 8m,
        };
        internalLine.Incentives.Add(warranty);

        await _sut.PriceAsync(quote);

        // Warranty: 8% of the 300 list price, per unit → 24 × 2 = 48, on the machines side.
        Assert.Equal(48m, warranty.Amount);
        Assert.Equal(1000m + 600m + 48m, quote.MachinesPrice);
    }

    [Fact]
    public async Task A_fixed_line_scheme_that_is_not_charged_is_only_recorded()
    {
        var cover = AddScheme(new IncentiveScheme
        {
            Code = "FREE_CHECKUP", Label = "Free check-up", Kind = IncentiveKinds.LineAmount, DefaultAmount = 20m, AddToPrice = false,
        });
        var quote = QuoteWith(VatCodes.Exempt, (External(1000m), ProductRole.ExternalUnit, 3));
        var line = new QuoteProductIncentive { IncentiveSchemeId = cover.Id, Mode = LineAmountModes.Fixed, Value = 20m };
        quote.Products.Single().Incentives.Add(line);

        await _sut.PriceAsync(quote);

        Assert.Equal(60m, line.Amount);
        Assert.Equal(3000m, quote.MachinesPrice);
    }

    [Fact]
    public async Task A_charged_quote_amount_is_added_after_vat_and_a_display_only_one_is_not()
    {
        var fee = AddScheme(new IncentiveScheme
        {
            Code = "SURVEY_FEE", Label = "Survey fee", Kind = IncentiveKinds.QuoteAmount, DefaultAmount = 80m, AddToPrice = true,
        });
        var quote = QuoteWith(VatCodes.Reduced10, (External(1000m, maxBtu: null), ProductRole.ExternalUnit, 1));
        quote.Incentives.Add(new QuoteIncentive { IncentiveSchemeId = fee.Id, Amount = 80m });
        AddIncentive(quote, IncentiveSchemeDefaults.WeeeTakeBack, 45m);

        await _sut.PriceAsync(quote);

        // 1000 × 1.1 = 1100 (no indoor unit, so no base installation), + 80 charged after VAT;
        // the WEEE amount is display-only.
        Assert.Equal(1180m, quote.FinalPrice);
    }

    [Fact]
    public async Task Refrigerant_gas_mismatch_is_rejected()
    {
        var quote = QuoteWith(VatCodes.Exempt,
            (External(gas: "R32"), ProductRole.ExternalUnit, 1),
            (Internal(gas: "R410A"), ProductRole.InternalUnit, 1));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.PriceAsync(quote));
        Assert.Contains("refrigerant", ex.Message);
    }

    [Fact]
    public async Task Internal_units_exceeding_external_capacity_are_rejected()
    {
        var quote = QuoteWith(VatCodes.Exempt,
            (External(maxBtu: 4000m), ProductRole.ExternalUnit, 1),
            (Internal(power: 2500m), ProductRole.InternalUnit, 1),
            (Internal(power: 2500m), ProductRole.InternalUnit, 1));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.PriceAsync(quote));
        Assert.Contains("capacity", ex.Message);
    }

    [Fact]
    public async Task Ducted_outlets_are_priced_per_outlet_and_added_after_the_vat_blend()
    {
        var quote = QuoteWith(VatCodes.Standard22, (External(1000m), ProductRole.ExternalUnit, 1));
        var ducted = new DuctedUnit();
        ducted.Outlets.Add(new DuctedUnitOutlet { Position = 1, SystemType = DuctSystemTypeCodes.Standard, VentCount = 4 });
        ducted.Outlets.Add(new DuctedUnitOutlet { Position = 2, SystemType = DuctSystemTypeCodes.Airzone, VentCount = 3 });
        quote.DuctedUnits.Add(ducted);

        await _sut.PriceAsync(quote);

        // Blend: (0 × 2.2) + (1000 − 0) × 1.22 = 1220
        // Standard 4 vents: (1030 + 2·190) × 1.1 = 1551 ; Airzone 3 vents: (2400 + 460) × 1.1 = 3146
        Assert.Equal(1220m + 1551m + 3146m, quote.FinalPrice);
        Assert.Equal(1000m, quote.MachinesPrice);
    }

    [Fact]
    public async Task Ducted_accessories_are_priced_from_service_items_into_the_machines_side()
    {
        _db.ServiceItems.AddRange(
            new ServiceItem { Code = InstallationCostItems.Plenum, Name = "Ducted plenum", Price = 120m },
            new ServiceItem { Code = InstallationCostItems.FlexibleHose, Name = "Flexible hose (m)", Price = 22m },
            new ServiceItem { Code = InstallationCostItems.RigidHose, Name = "Rigid duct (m)", Price = 28m });
        _db.SaveChanges();

        var quote = QuoteWith(VatCodes.Exempt, (External(1000m), ProductRole.ExternalUnit, 1));
        quote.DuctedUnits.Add(new DuctedUnit
        {
            PlenumQuantity = 2,
            HasFlexibleHose = true,
            FlexibleHoseQuantity = 5,
            HasRigidHose = false,
            RigidHoseQuantity = 99, // ignored: the rigid-hose flag is off
        });

        await _sut.PriceAsync(quote);

        Assert.Equal(1000m + 240m + 110m, quote.MachinesPrice);
    }

    [Fact]
    public async Task Missing_ducted_accessory_price_row_is_a_business_error()
    {
        var quote = QuoteWith(VatCodes.Exempt, (External(1000m), ProductRole.ExternalUnit, 1));
        quote.DuctedUnits.Add(new DuctedUnit { RemoteQuantity = 1 });

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.PriceAsync(quote));
        Assert.Contains(InstallationCostItems.Remote, ex.Message);
    }

    [Fact]
    public async Task Extras_split_between_vat_blended_installation_and_flat_lines()
    {
        var wiring = new ExtraType { Code = "WIRING", Label = "Wiring", DefaultPrice = 20m, QuantityBased = true };
        var wifi = new ExtraType { Code = "WIFI", Label = "WiFi", DefaultPrice = 50m, ExcludedFromVat = true };
        _db.ExtraTypes.AddRange(wiring, wifi);
        _db.SaveChanges();

        var quote = QuoteWith(VatCodes.Reduced10, (External(1000m), ProductRole.ExternalUnit, 1));
        quote.Extras.Add(new QuoteExtra { ExtraTypeId = wiring.Id, Quantity = 3, UnitPrice = 20m });
        quote.Extras.Add(new QuoteExtra { ExtraTypeId = wifi.Id, Quantity = 1, UnitPrice = 50m });

        await _sut.PriceAsync(quote);

        Assert.Equal(60m, quote.InstallationPrice);
        Assert.Equal((1000m + 60m) * 1.1m + 50m, quote.FinalPrice);
    }

    [Fact]
    public async Task Itemised_service_lines_add_to_installation()
    {
        var quote = QuoteWith(VatCodes.Exempt, (External(1000m), ProductRole.ExternalUnit, 1));
        quote.ServiceItems.Add(new QuoteServiceItem { Quantity = 2.5m, UnitPrice = 40m });

        await _sut.PriceAsync(quote);

        Assert.Equal(100m, quote.InstallationPrice);
        Assert.Equal(1100m, quote.FinalPrice);
    }

    [Fact]
    public async Task Manual_discount_and_rounding_adjust_the_final_price()
    {
        var quote = QuoteWith(VatCodes.Exempt, (External(1000m), ProductRole.ExternalUnit, 1));
        quote.ManualDiscount = -100m;
        quote.Rounding = 5m;

        await _sut.PriceAsync(quote);

        Assert.Equal(905m, quote.FinalPrice);
    }

    [Fact]
    public async Task Sf_price_grosses_below_2000_by_30_percent_and_bumps_to_next_50()
    {
        var quote = QuoteWith(VatCodes.Reduced10,
            (External(1000m), ProductRole.ExternalUnit, 1),
            (Internal(300m), ProductRole.InternalUnit, 1));
        var discount = AddIncentive(quote, IncentiveSchemeDefaults.InvoiceDiscount50);

        await _sut.PriceAsync(quote);

        // basis 1870 × 1.3 = 2431 → next €50 = 2450; the customer pays 50%
        Assert.Equal(2450m, discount.Amount);
        Assert.Equal(1225m, discount.CustomerAmount());
    }

    [Fact]
    public async Task Sf_price_basis_adds_back_manual_discount_and_includes_enea()
    {
        var quote = QuoteWith(VatCodes.Reduced10, (External(2000m, maxBtu: null), ProductRole.ExternalUnit, 1));
        var discount = AddIncentive(quote, IncentiveSchemeDefaults.InvoiceDiscount50);
        quote.ManualDiscount = -200m;
        AddIncentive(quote, IncentiveSchemeDefaults.EnergyTaxDeduction, 100m);

        await _sut.PriceAsync(quote);

        // Final = 2200 − 200 = 2000; basis = 2000 + 200 + 100 = 2300 ≥ 2000 → × 1.2 = 2760 → 2800
        Assert.Equal(2000m, quote.FinalPrice);
        Assert.Equal(2800m, discount.Amount);
    }

    [Fact]
    public async Task Sf_price_adds_a_full_50_even_on_an_exact_multiple_like_legacy()
    {
        _db.VatOptions.Add(new VatOption { Code = "Flat1", Label = "Flat ×1", BlendShape = VatBlendShapes.Flat, MachinesMultiplier = 1m });
        _db.SaveChanges();
        var quote = QuoteWith("Flat1", (External(2500m, maxBtu: null), ProductRole.ExternalUnit, 1));
        var discount = AddIncentive(quote, IncentiveSchemeDefaults.InvoiceDiscount50);

        await _sut.PriceAsync(quote);

        // 2500 × 1.2 = 3000 (exact multiple) → legacy still adds 50
        Assert.Equal(3050m, discount.Amount);
    }

    [Fact]
    public async Task Invoice_discount_is_null_on_a_vat_exempt_quote()
    {
        var exempt = QuoteWith(VatCodes.Exempt, (External(1000m), ProductRole.ExternalUnit, 1));
        var discount = AddIncentive(exempt, IncentiveSchemeDefaults.InvoiceDiscount50, amount: 999m);

        await _sut.PriceAsync(exempt);

        Assert.Null(discount.Amount);
        Assert.Null(discount.CustomerAmount());
    }

    [Fact]
    public async Task Invoice_discount_follows_the_rates_threshold_step_and_share_on_its_row()
    {
        var custom = AddScheme(new IncentiveScheme
        {
            Code = "INVOICE_DISCOUNT_65", Label = "65% invoice discount", Kind = IncentiveKinds.InvoiceDiscount,
            Threshold = 5000m, RateBelowThreshold = 1.5m, RateAboveThreshold = 1.4m, RoundingStep = 100m, Percent = 35m,
            RequiresVat = true,
        });
        var quote = QuoteWith(VatCodes.Reduced10, (External(2000m, maxBtu: null), ProductRole.ExternalUnit, 1));
        var discount = new QuoteIncentive { IncentiveSchemeId = custom.Id };
        quote.Incentives.Add(discount);

        await _sut.PriceAsync(quote);

        // Final 2000 × 1.1 = 2200 < 5000 → × 1.5 = 3300, an exact multiple, so a full step is still
        // added (legacy rule) → 3400; the customer pays 35% = 1190.
        Assert.Equal(3400m, discount.Amount);
        Assert.Equal(1190m, discount.CustomerAmount());
    }

    [Fact]
    public async Task Unknown_vat_option_is_rejected()
    {
        var quote = QuoteWith("Nope", (External(1000m), ProductRole.ExternalUnit, 1));

        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.PriceAsync(quote));
    }

    [Fact]
    public async Task Unrecognised_blend_shape_is_rejected()
    {
        _db.VatOptions.Add(new VatOption { Code = "Weird", Label = "Weird", BlendShape = "Diagonal" });
        _db.SaveChanges();
        var quote = QuoteWith("Weird", (External(1000m), ProductRole.ExternalUnit, 1));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.PriceAsync(quote));
        Assert.Contains("blend shape", ex.Message);
    }
}
