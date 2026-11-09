using HvacErp.Application.Dtos;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;

namespace HvacErp.Application.Erp;

/// <summary>
/// Builds the <see cref="ErpOrder"/> for a quote. The quote must be loaded with Client, Operator,
/// Products (with Product), ServiceItems (with ServiceItem), Extras (with ExtraType), Incentives
/// (with IncentiveScheme) and StatusEvents (see <see cref="IncludeForErp"/>).
/// </summary>
public static class ErpOrderMapper
{
    /// <summary>The navigation paths <see cref="FromQuote"/> reads.</summary>
    public static readonly string[] IncludeForErp =
    [
        nameof(Quote.Client),
        nameof(Quote.Operator),
        $"{nameof(Quote.Products)}.{nameof(QuoteProduct.Product)}",
        $"{nameof(Quote.ServiceItems)}.{nameof(QuoteServiceItem.ServiceItem)}",
        $"{nameof(Quote.Extras)}.{nameof(QuoteExtra.ExtraType)}",
        $"{nameof(Quote.Incentives)}.{nameof(QuoteIncentive.IncentiveScheme)}",
        nameof(Quote.StatusEvents),
    ];

    /// <summary>
    /// The ERP order document for a quote. <paramref name="confirmedAt"/> overrides the confirmation
    /// time read from the status history (for a confirmation not saved yet).
    /// </summary>
    public static ErpOrder FromQuote(Quote quote, string? vatLabel, DateTime? confirmedAt = null)
    {
        var lines = new List<ErpOrderLine>();
        var lineNumber = 0;

        foreach (var p in quote.Products.OrderBy(p => p.Role == ProductRole.ExternalUnit ? 0 : 1).ThenBy(p => p.Id))
        {
            var role = p.Role == ProductRole.ExternalUnit ? "Outdoor unit" : "Indoor unit";
            lines.Add(new ErpOrderLine(
                ++lineNumber, ErpLineKinds.Product, p.Product.Ean,
                $"{role}: {p.Product.Brand} {p.Product.Model}",
                p.Quantity, p.Price, p.DiscountedPrice, Round(p.DiscountedPrice * p.Quantity)));
        }

        foreach (var s in quote.ServiceItems.OrderBy(s => s.Id))
        {
            lines.Add(new ErpOrderLine(
                ++lineNumber, ErpLineKinds.Service, s.ServiceItem.Code, s.ServiceItem.Name,
                s.Quantity, s.UnitPrice, s.UnitPrice, Round(s.UnitPrice * s.Quantity)));
        }

        foreach (var e in quote.Extras.OrderBy(e => e.ExtraType.SortOrder))
        {
            lines.Add(new ErpOrderLine(
                ++lineNumber, ErpLineKinds.Extra, e.ExtraType.Code, e.ExtraType.Label,
                e.Quantity, e.UnitPrice, e.UnitPrice, Round(e.UnitPrice * e.Quantity)));
        }

        // Quote amounts (fees, the warranty); the invoice discount is in the totals, not a line.
        foreach (var i in quote.Incentives
                     .Where(i => i.IncentiveScheme.Kind == IncentiveKinds.QuoteAmount && i.Amount is not null)
                     .OrderBy(i => i.IncentiveScheme.SortOrder))
        {
            var amount = i.Amount!.Value;
            lines.Add(new ErpOrderLine(
                ++lineNumber, ErpLineKinds.Incentive, i.IncentiveScheme.Code, i.IncentiveScheme.Label,
                1m, amount, amount, amount));
        }

        var discount = quote.Incentives.FirstOrDefault(i => i.IncentiveScheme.Kind == IncentiveKinds.InvoiceDiscount);
        confirmedAt ??= quote.StatusEvents
            .Where(e => e.Status == QuoteStatusCodes.Confirmed)
            .OrderByDescending(e => e.OccurredAt)
            .Select(e => (DateTime?)e.OccurredAt)
            .FirstOrDefault();

        var client = quote.Client;
        var site = string.IsNullOrWhiteSpace(quote.InstallationAddress)
            ? null
            : new ErpAddress(quote.InstallationAddress, quote.InstallationCity, null, null);

        return new ErpOrder(
            ErpOrder.CurrentSchemaVersion,
            ExternalReference: $"quote-{quote.Id}",
            QuoteNumber: quote.DisplayNumber(),
            QuoteId: quote.Id,
            QuoteDate: quote.CreatedAt,
            ConfirmedAt: confirmedAt,
            Agent: quote.Operator?.Name,
            Department: quote.Department,
            Currency: "EUR",
            Vat: new ErpVat(quote.Vat, vatLabel),
            Customer: new ErpCustomer(
                client.Code, client.FullName, client.TaxCode,
                client.Address is null && client.City is null ? null : new ErpAddress(client.Address, client.City, null, null),
                client.Phone, client.Mobile, client.Email),
            InstallationSite: site,
            Lines: lines,
            Totals: new ErpTotals(
                quote.MachinesPrice, quote.InstallationPrice, quote.ManualDiscount, quote.Rounding, quote.FinalPrice,
                discount?.Amount, discount?.CustomerAmount()),
            Notes: quote.InstallationPositionNotes);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
