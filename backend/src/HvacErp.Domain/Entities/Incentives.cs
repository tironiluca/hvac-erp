namespace HvacErp.Domain.Entities;

/// <summary>
/// A tax incentive, fee or optional cover a quote can carry (e.g. an energy-efficiency deduction,
/// equipment take-back, an invoice-discount scheme, an extended warranty). The row says how the
/// amount is calculated: <see cref="Kind"/> picks one of the built-in calculations and the
/// parameter columns feed it, so a rate or threshold change is a row edit, not a release.
/// </summary>
public class IncentiveScheme : ITenantOwned
{
    public int TenantId { get; set; }

    public int Id { get; set; }

    /// <summary>Stable machine key (e.g. <c>INVOICE_DISCOUNT_50</c>).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Shown in the quote wizard.</summary>
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>The calculation, one of <see cref="IncentiveKinds"/>.</summary>
    public string Kind { get; set; } = IncentiveKinds.QuoteAmount;

    /// <summary>
    /// <see cref="IncentiveKinds.QuoteAmount"/>: the € amount proposed when staff pick
    /// <see cref="AmountModes.Fixed"/> on a quote.
    /// </summary>
    public decimal? DefaultAmount { get; set; }

    /// <summary>
    /// <see cref="IncentiveKinds.QuoteAmount"/>: the percentage of the quote's equipment list total
    /// proposed when staff pick <see cref="AmountModes.Percent"/>; when set, a newly ticked scheme
    /// starts in that mode. <see cref="IncentiveKinds.InvoiceDiscount"/>: the share of the grossed-up
    /// total the customer pays.
    /// </summary>
    public decimal? Percent { get; set; }

    /// <summary>Whether the amount is charged. False: the amount is only shown on the quote.</summary>
    public bool AddToPrice { get; set; }

    /// <summary>
    /// A charged quote amount (<see cref="AddToPrice"/>) goes on the machines side, before the VAT
    /// blend (e.g. an extended warranty, which is equipment cover). False: it is added after VAT.
    /// </summary>
    public bool ChargeBeforeVat { get; set; }

    /// <summary>A quote amount that is added to the invoice-discount basis (e.g. the deduction paperwork fee).</summary>
    public bool IncludeInInvoiceDiscountBasis { get; set; }

    /// <summary>Unavailable on VAT-exempt quotes (the invoice discount then computes to nothing).</summary>
    public bool RequiresVat { get; set; }

    /// <summary>The customer has to accept it separately; staff toggle the acceptance on the quote.</summary>
    public bool RequiresConfirmation { get; set; }

    /// <summary><see cref="IncentiveKinds.InvoiceDiscount"/>: basis below which <see cref="RateBelowThreshold"/> applies.</summary>
    public decimal? Threshold { get; set; }

    /// <summary><see cref="IncentiveKinds.InvoiceDiscount"/>: gross-up multiplier for a basis below <see cref="Threshold"/>.</summary>
    public decimal? RateBelowThreshold { get; set; }

    /// <summary><see cref="IncentiveKinds.InvoiceDiscount"/>: gross-up multiplier from <see cref="Threshold"/> up.</summary>
    public decimal? RateAboveThreshold { get; set; }

    /// <summary><see cref="IncentiveKinds.InvoiceDiscount"/>: the grossed-up total is raised to the next multiple of this step.</summary>
    public decimal? RoundingStep { get; set; }

    /// <summary>Printed in the quote PDF's pricing summary; for an invoice discount, the block title.</summary>
    public string? PdfLabel { get; set; }

    /// <summary>Printed inside the invoice-discount block when the quote carries this scheme.</summary>
    public string? PdfInvoiceDiscountNote { get; set; }

    public int SortOrder { get; set; }
    public bool Active { get; set; } = true;
}

/// <summary>The built-in calculations an <see cref="IncentiveScheme"/> can use.</summary>
public static class IncentiveKinds
{
    /// <summary>
    /// One amount per quote, set as staff choose: a fixed € amount or a percentage of the quote's
    /// equipment list total (<see cref="AmountModes"/>). Shown, and optionally charged.
    /// </summary>
    public const string QuoteAmount = "QuoteAmount";

    /// <summary>
    /// The quote total grossed up by a threshold-dependent rate and rounded up to the next step; the
    /// customer pays <see cref="IncentiveScheme.Percent"/> of it.
    /// </summary>
    public const string InvoiceDiscount = "InvoiceDiscount";

    /// <summary>All kinds, for validation.</summary>
    public static readonly IReadOnlyList<string> All = [QuoteAmount, InvoiceDiscount];
}

/// <summary>How a <see cref="IncentiveKinds.QuoteAmount"/> is set on a quote.</summary>
public static class AmountModes
{
    /// <summary><see cref="QuoteIncentive.Value"/> € for the whole quote.</summary>
    public const string Fixed = "Fixed";

    /// <summary><see cref="QuoteIncentive.Value"/> % of the quote's equipment list total (list unit price × quantity, all units).</summary>
    public const string Percent = "Percent";

    /// <summary>All modes, for validation.</summary>
    public static readonly IReadOnlyList<string> All = [Fixed, Percent];
}

/// <summary>An <see cref="IncentiveScheme"/> on a quote.</summary>
public class QuoteIncentive : ITenantOwned
{
    public int TenantId { get; set; }

    public int Id { get; set; }

    public int QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;

    public int IncentiveSchemeId { get; set; }
    public IncentiveScheme IncentiveScheme { get; set; } = null!;

    /// <summary><see cref="IncentiveKinds.QuoteAmount"/>: one of <see cref="AmountModes"/>; null for computed kinds.</summary>
    public string? Mode { get; set; }

    /// <summary><see cref="IncentiveKinds.QuoteAmount"/>: the € amount or the percentage, per <see cref="Mode"/>.</summary>
    public decimal? Value { get; set; }

    /// <summary>
    /// The resulting amount, computed by the pricing engine. <see cref="IncentiveKinds.InvoiceDiscount"/>:
    /// the grossed-up total; null when the scheme needs VAT and the quote is exempt.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>Customer acceptance, for schemes with <see cref="IncentiveScheme.RequiresConfirmation"/>.</summary>
    public bool Confirmed { get; set; }
}
