namespace HvacErp.Domain.Entities;

/// <summary>
/// A tax incentive, fee or optional cover a quote can carry (e.g. an energy-efficiency deduction,
/// equipment take-back, an invoice-discount scheme, an extended warranty). The row says how the
/// amount is calculated: <see cref="Kind"/> picks one of the built-in calculations and the
/// parameter columns feed it, so a rate or threshold change is a row edit, not a release.
/// </summary>
public class IncentiveScheme
{
    public int Id { get; set; }

    /// <summary>Stable machine key (e.g. <c>INVOICE_DISCOUNT_50</c>).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Shown in the quote wizard.</summary>
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>The calculation, one of <see cref="IncentiveKinds"/>.</summary>
    public string Kind { get; set; } = IncentiveKinds.QuoteAmount;

    /// <summary>
    /// <see cref="IncentiveKinds.QuoteAmount"/>: amount proposed for a new quote (staff can change it
    /// per quote). <see cref="IncentiveKinds.LineAmount"/>: the fixed amount per unit proposed when
    /// staff pick <see cref="LineAmountModes.Fixed"/> on a line.
    /// </summary>
    public decimal? DefaultAmount { get; set; }

    /// <summary>
    /// <see cref="IncentiveKinds.LineAmount"/>: the percentage of the list unit price proposed when
    /// staff pick <see cref="LineAmountModes.Percent"/> on a line; when set, a newly ticked line starts
    /// in that mode. <see cref="IncentiveKinds.InvoiceDiscount"/>: the share of the grossed-up total
    /// the customer pays.
    /// </summary>
    public decimal? Percent { get; set; }

    /// <summary>
    /// Whether the amount is charged. A quote amount is added to the final price after VAT; a line
    /// amount is added to the machines side, before the VAT blend. False: the amount is only shown.
    /// </summary>
    public bool AddToPrice { get; set; }

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

    /// <summary>Printed inside the invoice-discount block when the quote (or one of its lines) carries this scheme.</summary>
    public string? PdfInvoiceDiscountNote { get; set; }

    public int SortOrder { get; set; }
    public bool Active { get; set; } = true;
}

/// <summary>The built-in calculations an <see cref="IncentiveScheme"/> can use.</summary>
public static class IncentiveKinds
{
    /// <summary>One amount per quote (entered or defaulted), shown and optionally charged.</summary>
    public const string QuoteAmount = "QuoteAmount";

    /// <summary>One amount per product line: a fixed amount or a percentage of the list price, per unit.</summary>
    public const string LineAmount = "LineAmount";

    /// <summary>
    /// The quote total grossed up by a threshold-dependent rate and rounded up to the next step; the
    /// customer pays <see cref="IncentiveScheme.Percent"/> of it.
    /// </summary>
    public const string InvoiceDiscount = "InvoiceDiscount";

    /// <summary>All kinds, for validation.</summary>
    public static readonly IReadOnlyList<string> All = [QuoteAmount, LineAmount, InvoiceDiscount];
}

/// <summary>A quote-level <see cref="IncentiveScheme"/> on a quote.</summary>
public class QuoteIncentive
{
    public int Id { get; set; }

    public int QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;

    public int IncentiveSchemeId { get; set; }
    public IncentiveScheme IncentiveScheme { get; set; } = null!;

    /// <summary>
    /// <see cref="IncentiveKinds.QuoteAmount"/>: the amount (entered, else the scheme default).
    /// <see cref="IncentiveKinds.InvoiceDiscount"/>: the grossed-up total computed by the pricing
    /// engine; null when the scheme needs VAT and the quote is exempt.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>Customer acceptance, for schemes with <see cref="IncentiveScheme.RequiresConfirmation"/>.</summary>
    public bool Confirmed { get; set; }
}

/// <summary>How a <see cref="QuoteProductIncentive"/>'s amount is set on its line.</summary>
public static class LineAmountModes
{
    /// <summary><see cref="QuoteProductIncentive.Value"/> % of the list unit price, per unit.</summary>
    public const string Percent = "Percent";

    /// <summary><see cref="QuoteProductIncentive.Value"/> € per unit.</summary>
    public const string Fixed = "Fixed";

    /// <summary>All modes, for validation.</summary>
    public static readonly IReadOnlyList<string> All = [Percent, Fixed];
}

/// <summary>
/// A line-level <see cref="IncentiveScheme"/> (<see cref="IncentiveKinds.LineAmount"/>, e.g. an
/// extended warranty) on a quote line. Staff choose per line between a percentage of the list price
/// and a fixed amount per unit; the scheme only supplies the proposed default.
/// </summary>
public class QuoteProductIncentive
{
    public int Id { get; set; }

    public int QuoteProductId { get; set; }
    public QuoteProduct QuoteProduct { get; set; } = null!;

    public int IncentiveSchemeId { get; set; }
    public IncentiveScheme IncentiveScheme { get; set; } = null!;

    /// <summary>One of <see cref="LineAmountModes"/>.</summary>
    public string Mode { get; set; } = LineAmountModes.Percent;

    /// <summary>The percentage (<see cref="LineAmountModes.Percent"/>) or the € per unit (<see cref="LineAmountModes.Fixed"/>).</summary>
    public decimal Value { get; set; }

    /// <summary>The line's amount, computed by the pricing engine (all units of the line).</summary>
    public decimal Amount { get; set; }
}
