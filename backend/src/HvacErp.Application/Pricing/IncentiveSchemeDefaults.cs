using HvacErp.Domain.Entities;

namespace HvacErp.Application.Pricing;

/// <summary>
/// The incentive schemes a new installation starts with, as editable examples of each calculation
/// kind: an energy-efficiency tax deduction (a paperwork fee in the invoice-discount basis), an
/// equipment take-back note, a 50% invoice discount, and a per-line extended warranty. A deployment
/// renames, re-prices or deactivates them as data.
/// </summary>
public static class IncentiveSchemeDefaults
{
    public const string EnergyTaxDeduction = "ENERGY_TAX_DEDUCTION";
    public const string WeeeTakeBack = "WEEE_TAKE_BACK";
    public const string InvoiceDiscount50 = "INVOICE_DISCOUNT_50";
    public const string AllInclusiveWarranty = "ALL_INCLUSIVE_WARRANTY";

    /// <summary>The default schemes, new instances on every call.</summary>
    public static List<IncentiveScheme> All() =>
    [
        new()
        {
            Code = EnergyTaxDeduction,
            Label = "Energy-efficiency tax deduction",
            Description = "Paperwork fee for the customer's tax deduction; counts towards the invoice-discount basis.",
            Kind = IncentiveKinds.QuoteAmount,
            IncludeInInvoiceDiscountBasis = true,
            PdfLabel = "Eligible for the energy-efficiency tax deduction",
            PdfInvoiceDiscountNote = "Tax-deduction paperwork included",
            SortOrder = 10,
        },
        new()
        {
            Code = WeeeTakeBack,
            Label = "WEEE take-back",
            Description = "Collection and disposal of the replaced equipment.",
            Kind = IncentiveKinds.QuoteAmount,
            PdfLabel = "WEEE collection and disposal included",
            SortOrder = 20,
        },
        new()
        {
            Code = InvoiceDiscount50,
            Label = "50% invoice discount",
            Description = "Total grossed up ×1.3 below €2,000 (×1.2 from €2,000), raised to the next €50; the customer pays 50%.",
            Kind = IncentiveKinds.InvoiceDiscount,
            Percent = 50m,
            Threshold = 2000m,
            RateBelowThreshold = 1.3m,
            RateAboveThreshold = 1.2m,
            RoundingStep = 50m,
            RequiresVat = true,
            RequiresConfirmation = true,
            PdfLabel = "INVOICE DISCOUNT",
            SortOrder = 30,
        },
        new()
        {
            Code = AllInclusiveWarranty,
            Label = "All-inclusive warranty",
            Description = "Extended warranty per unit, priced at 8% of the unit's list price.",
            Kind = IncentiveKinds.LineAmount,
            Percent = 8m,
            AddToPrice = true,
            PdfInvoiceDiscountNote = "All-inclusive warranty included",
            SortOrder = 40,
        },
    ];
}
