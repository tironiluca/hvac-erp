using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Interfaces;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;

namespace HvacErp.Application.Services;

/// <summary>
/// <see cref="ServiceItem.Code"/>s of the service items the pricing engine reads: the base
/// installation price per internal-unit count, and the ducted-system accessory unit prices (legacy
/// costiinstallazione rows read by InsPrev.vb calcolaExtraCanalizzato()). Each must exist or pricing
/// a quote fails. The frontend price preview mirrors these codes (quote-price-estimate.ts).
/// </summary>
public static class InstallationCostItems
{
    /// <summary>Code of the base installation price for a system with <paramref name="internalUnitCount"/> internal units (1–5+).</summary>
    public static string BaseSystemPriceFor(int internalUnitCount) => $"BASE_SYSTEM_{Math.Clamp(internalUnitCount, 1, 5)}";

    public const string Plenum = "DUCTED_PLENUM";
    public const string Cloud = "DUCTED_CLOUD_MODULE";
    public const string FlexibleHose = "DUCTED_FLEXIBLE_HOSE_M";
    public const string RigidHose = "DUCTED_RIGID_HOSE_M";
    public const string Remote = "DUCTED_REMOTE";
}

/// <summary>Default <see cref="Interfaces.IQuotePricingService"/> — the port of the legacy InsPrev.vb engine.</summary>
public class QuotePricingService(IAppDbContext context) : IQuotePricingService
{
    public async Task PriceAsync(Quote quote, CancellationToken cancellationToken = default)
    {
        var productIds = quote.Products.Select(p => p.ProductId).Distinct().ToList();
        var products = await context.Products
            .Include(p => p.PriceList).ThenInclude(pl => pl.DiscountTiers)
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        ValidateCompatibility(quote, products);
        var schemes = await LoadIncentiveSchemesAsync(quote, cancellationToken);

        var machinesPrice = PriceUnits(quote, products, schemes)
                            + await PriceDuctedAccessoriesAsync(quote, cancellationToken);
        var (extrasInstallation, extrasFlat) = await PriceExtrasAsync(quote, cancellationToken);
        var installationPrice = await PriceInstallationAsync(quote, cancellationToken) + extrasInstallation;

        // Per-outlet ducted-system price. The ×1.1 in the formula IS the VAT, so legacy
        // InsPrev.vb calcolaPrezzo adds this to the grand total flat, AFTER the VAT blend
        // (`prezzo += prezzoCanalizzato`) — it is never run through a blend multiplier.
        var ductedOutletsPrice = PriceDuctedOutlets(quote);

        var vat = await context.VatOptions.FindAsync([quote.Vat], cancellationToken)
            ?? throw new BusinessRuleException($"Unknown VAT option '{quote.Vat}'.");

        // The multipliers are seeded to reproduce the legacy literals exactly (Standard22: machines
        // ×1.22, installation ×2.2; Reduced10: ×1.1; Exempt: ×1) — a tax-rate change is now a row edit.
        var blended = vat.BlendShape switch
        {
            VatBlendShapes.Split => (installationPrice * vat.InstallationMultiplier)
                                    + ((machinesPrice - installationPrice) * vat.MachinesMultiplier),
            VatBlendShapes.Flat => (machinesPrice + installationPrice) * vat.MachinesMultiplier,
            VatBlendShapes.None => machinesPrice + installationPrice,
            _ => throw new BusinessRuleException($"VAT option '{quote.Vat}' has an unrecognised blend shape '{vat.BlendShape}'.")
        };

        quote.MachinesPrice = machinesPrice;
        quote.InstallationPrice = installationPrice;
        quote.FinalPrice = blended + extrasFlat + ductedOutletsPrice + ChargedQuoteAmounts(quote, schemes)
                           + quote.ManualDiscount + quote.Rounding;
        PriceInvoiceDiscounts(quote, schemes, vat);
    }

    private async Task<Dictionary<int, IncentiveScheme>> LoadIncentiveSchemesAsync(Quote quote, CancellationToken cancellationToken)
    {
        var ids = quote.Incentives.Select(i => i.IncentiveSchemeId)
            .Concat(quote.Products.SelectMany(p => p.Incentives).Select(i => i.IncentiveSchemeId))
            .Distinct()
            .ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var schemes = await context.IncentiveSchemes.Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, cancellationToken);

        // Rows built from a request carry only the id; give them their scheme for the callers that follow.
        foreach (var incentive in quote.Incentives)
        {
            incentive.IncentiveScheme = schemes[incentive.IncentiveSchemeId];
        }
        foreach (var incentive in quote.Products.SelectMany(p => p.Incentives))
        {
            incentive.IncentiveScheme = schemes[incentive.IncentiveSchemeId];
        }

        return schemes;
    }

    /// <summary>
    /// <see cref="IncentiveKinds.LineAmount"/>: a fixed amount per unit, or <see cref="IncentiveScheme.Percent"/>
    /// of the list unit price per unit (legacy all-inclusive warranty, 8% of list).
    /// </summary>
    public static decimal LineAmount(IncentiveScheme scheme, decimal listUnitPrice, int quantity) =>
        Math.Round(
            (scheme.DefaultAmount ?? listUnitPrice * (scheme.Percent ?? 0m) / 100m) * quantity,
            2, MidpointRounding.AwayFromZero);

    /// <summary>Quote amounts charged on top of the VAT-blended total (<see cref="IncentiveScheme.AddToPrice"/>).</summary>
    private static decimal ChargedQuoteAmounts(Quote quote, IReadOnlyDictionary<int, IncentiveScheme> schemes) =>
        quote.Incentives
            .Where(i => schemes[i.IncentiveSchemeId] is { Kind: IncentiveKinds.QuoteAmount, AddToPrice: true })
            .Sum(i => i.Amount ?? 0m);

    /// <summary>
    /// <see cref="IncentiveKinds.InvoiceDiscount"/> (legacy <c>InsPrev.vb</c> <c>prezzoSF</c>). The basis is
    /// the final price with <see cref="Quote.ManualDiscount"/> added back out (legacy grosses the
    /// pre-discount <c>txtPrezzoFinale = txtPrezzo + txtScontoPrezzo</c>), plus every quote amount
    /// flagged <see cref="IncentiveScheme.IncludeInInvoiceDiscountBasis"/>. Line amounts are already in
    /// <see cref="Quote.FinalPrice"/>. The basis is grossed up by the below / above-threshold rate and
    /// then raised to the next rounding step; legacy adds a full step even on an exact multiple, kept
    /// as-is. Null when the scheme needs VAT and the quote is exempt.
    /// </summary>
    private static void PriceInvoiceDiscounts(Quote quote, IReadOnlyDictionary<int, IncentiveScheme> schemes, VatOption vat)
    {
        var discounts = quote.Incentives.Where(i => schemes[i.IncentiveSchemeId].Kind == IncentiveKinds.InvoiceDiscount);
        foreach (var discount in discounts)
        {
            var scheme = schemes[discount.IncentiveSchemeId];
            if (scheme.RequiresVat && vat.BlendShape == VatBlendShapes.None)
            {
                discount.Amount = null;
                continue;
            }

            var basis = quote.FinalPrice - quote.ManualDiscount
                        + quote.Incentives
                            .Where(i => schemes[i.IncentiveSchemeId] is { Kind: IncentiveKinds.QuoteAmount, IncludeInInvoiceDiscountBasis: true })
                            .Sum(i => i.Amount ?? 0m);
            discount.Amount = InvoiceDiscountTotal(scheme, basis);
        }
    }

    /// <summary>The grossed-up, step-rounded invoice total for a basis (see <see cref="PriceInvoiceDiscounts"/>).</summary>
    public static decimal InvoiceDiscountTotal(IncentiveScheme scheme, decimal basis)
    {
        var rate = basis < scheme.Threshold!.Value ? scheme.RateBelowThreshold!.Value : scheme.RateAboveThreshold!.Value;
        var grossed = basis * rate;
        var step = scheme.RoundingStep!.Value;
        return grossed + (step - grossed % step);
    }

    /// <summary>
    /// Internal units must share the external unit's refrigerant gas (hydronic systems are exempt),
    /// and their combined rated power must not exceed the external unit's rated capacity.
    /// </summary>
    private static void ValidateCompatibility(Quote quote, IReadOnlyDictionary<int, Product> products)
    {
        var external = quote.Products
            .Where(p => p.Role == ProductRole.ExternalUnit)
            .Select(p => products.GetValueOrDefault(p.ProductId))
            .FirstOrDefault();

        if (external is null)
        {
            return;
        }

        var isHydronic = external.IsHydronic;

        var internalUnits = quote.Products
            .Where(p => p.Role == ProductRole.InternalUnit)
            .Select(p => (Line: p, Product: products.GetValueOrDefault(p.ProductId)))
            .Where(x => x.Product is not null)
            .ToList();

        if (!isHydronic && !string.IsNullOrWhiteSpace(external.RefrigerantGas))
        {
            var mismatched = internalUnits
                .Where(x => !string.Equals(x.Product!.RefrigerantGas, external.RefrigerantGas, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (mismatched.Count > 0)
            {
                throw new BusinessRuleException(
                    $"Internal unit(s) use a different refrigerant gas than the external unit ({external.RefrigerantGas}).");
            }
        }

        if (external.MaxBtu is { } maxBtu)
        {
            var totalPower = internalUnits.Sum(x => x.Product!.Power ?? 0m);
            if (totalPower > maxBtu)
            {
                throw new BusinessRuleException(
                    $"Selected internal units exceed the external unit's rated capacity ({totalPower} > {maxBtu}).");
            }
        }
    }

    private static decimal PriceUnits(
        Quote quote, IReadOnlyDictionary<int, Product> products, IReadOnlyDictionary<int, IncentiveScheme> schemes)
    {
        decimal total = 0m;

        foreach (var line in quote.Products)
        {
            var product = products.GetValueOrDefault(line.ProductId);
            if (product is null)
            {
                continue;
            }

            var isHydronic = product.IsHydronic;
            var listPrice = isHydronic
                ? product.HydronicPrice ?? product.Price
                : quote.Predisposed
                    ? product.PricePrepared ?? product.Price
                    : product.PriceNotPrepared ?? product.Price;

            var netPrice = ApplyDiscountLadder(listPrice, product.PriceList?.DiscountTiers);

            line.Price = listPrice;
            line.DiscountedPrice = netPrice;

            total += netPrice * line.Quantity;
            foreach (var incentive in line.Incentives)
            {
                var scheme = schemes[incentive.IncentiveSchemeId];
                incentive.Amount = LineAmount(scheme, listPrice, line.Quantity);
                if (scheme.AddToPrice)
                {
                    total += incentive.Amount;
                }
            }
        }

        return total;
    }

    /// <summary>
    /// Applies a price list's cascading discount ladder (legacy <c>ListiniProdotti.Sconto1..6</c>,
    /// now <see cref="PriceListDiscountTier"/>) in <see cref="PriceListDiscountTier.TierOrder"/> order:
    /// each tier's percentage comes off the running balance, not the original list price.
    /// </summary>
    private static decimal ApplyDiscountLadder(decimal listPrice, IEnumerable<PriceListDiscountTier>? tiers)
    {
        if (tiers is null)
        {
            return listPrice;
        }

        var net = listPrice;
        foreach (var tier in tiers.OrderBy(t => t.TierOrder))
        {
            net *= 1m - (tier.Percentage / 100m);
        }

        return net;
    }

    /// <summary>
    /// Sums the quote's <see cref="QuoteExtra"/> lines, split by whether the extra is billed inside
    /// the VAT-blended total (most work items) or flat outside it (legacy WiFi-module behaviour).
    /// </summary>
    private async Task<(decimal Installation, decimal Flat)> PriceExtrasAsync(Quote quote, CancellationToken cancellationToken)
    {
        if (quote.Extras.Count == 0)
        {
            return (0m, 0m);
        }

        var typeIds = quote.Extras.Select(e => e.ExtraTypeId).Distinct().ToList();
        var types = await context.ExtraTypes
            .Where(t => typeIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        decimal installation = 0m;
        decimal flat = 0m;

        foreach (var extra in quote.Extras)
        {
            if (!types.TryGetValue(extra.ExtraTypeId, out var type))
            {
                continue;
            }

            var line = extra.Quantity * extra.UnitPrice;
            if (type.ExcludedFromVat)
            {
                flat += line;
            }
            else
            {
                installation += line;
            }
        }

        return (installation, flat);
    }

    /// <summary>
    /// Ducted-system accessories (plenum, cloud module, flexible/rigid hose metres, remote) priced
    /// from their <see cref="ServiceItem"/> rows and summed into the machines side of the VAT blend —
    /// legacy <c>InsPrev.vb</c> <c>calcolaExtraCanalizzato()</c> adds the total into <c>prezzoMacchine</c>.
    /// </summary>
    private async Task<decimal> PriceDuctedAccessoriesAsync(Quote quote, CancellationToken cancellationToken)
    {
        var lines = quote.DuctedUnits
            .SelectMany(d => new (string Code, int Quantity)[]
            {
                (InstallationCostItems.Plenum, d.PlenumQuantity),
                (InstallationCostItems.Cloud, d.CloudQuantity),
                (InstallationCostItems.Remote, d.RemoteQuantity),
                (InstallationCostItems.FlexibleHose, d.HasFlexibleHose ? d.FlexibleHoseQuantity : 0),
                (InstallationCostItems.RigidHose, d.HasRigidHose ? d.RigidHoseQuantity : 0),
            })
            .Where(x => x.Quantity > 0)
            .ToList();

        if (lines.Count == 0)
        {
            return 0m;
        }

        var codes = lines.Select(l => l.Code).Distinct().ToList();
        var prices = await context.ServiceItems
            .Where(s => s.Code != null && codes.Contains(s.Code))
            .ToDictionaryAsync(s => s.Code!, s => s.Price, cancellationToken);

        decimal total = 0m;
        foreach (var (code, quantity) in lines)
        {
            if (!prices.TryGetValue(code, out var unitPrice))
            {
                throw new BusinessRuleException(
                    $"Price list is missing the required service item with code '{code}' — seed it before quoting.");
            }

            total += unitPrice * quantity;
        }

        return total;
    }

    private async Task<decimal> PriceInstallationAsync(Quote quote, CancellationToken cancellationToken)
    {
        decimal total = 0m;

        var internalUnitCount = quote.Products.Count(p => p.Role == ProductRole.InternalUnit);
        if (internalUnitCount > 0)
        {
            total += await GetServiceItemPriceAsync(InstallationCostItems.BaseSystemPriceFor(internalUnitCount), cancellationToken);
        }

        foreach (var item in quote.ServiceItems)
        {
            total += item.Quantity * item.UnitPrice;
        }

        return total;
    }

    /// <summary>
    /// Per-outlet ducted-system price: each ducted indoor unit carries its own Standard/Airzone type
    /// and its own vent count (legacy <c>InsPrev.vb</c> <c>calcolaPrezzo</c> loops <c>cmbCan{n}</c>
    /// per outlet). The ×1.1 is embedded VAT, so the caller adds the result to the grand total flat,
    /// after the VAT blend — never through a blend multiplier.
    /// </summary>
    private static decimal PriceDuctedOutlets(Quote quote)
    {
        decimal total = 0m;

        foreach (var ducted in quote.DuctedUnits)
        {
            foreach (var outlet in ducted.Outlets)
            {
                total += outlet.SystemType switch
                {
                    DuctSystemTypeCodes.Standard => (1030m + (outlet.VentCount - 2) * 190m) * 1.1m,
                    DuctSystemTypeCodes.Airzone => (2400m + (outlet.VentCount - 2) * 460m) * 1.1m,
                    _ => 0m
                };
            }
        }

        return total;
    }

    private async Task<decimal> GetServiceItemPriceAsync(string code, CancellationToken cancellationToken)
    {
        var price = await context.ServiceItems
            .Where(s => s.Code == code)
            .Select(s => (decimal?)s.Price)
            .FirstOrDefaultAsync(cancellationToken);

        if (price is null)
        {
            throw new BusinessRuleException(
                $"Price list is missing the required service item with code '{code}' — seed it before quoting.");
        }

        return price.Value;
    }
}
