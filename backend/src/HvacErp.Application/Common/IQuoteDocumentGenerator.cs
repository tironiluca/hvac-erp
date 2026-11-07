using HvacErp.Domain.Entities;

namespace HvacErp.Application.Common;

/// <summary>
/// Renders a customer-facing PDF for a quote. The caller must pass a <see cref="Quote"/> with
/// Client, Products (+Product), DuctedUnits, and ServiceItems (+ServiceItem) eager-loaded, the
/// resolved VAT line label (from the <c>vat_options</c> lookup — it is data, not a hard-coded
/// switch), and a <see cref="PdfRenderContext"/> holding the active template + branding.
/// </summary>
public interface IQuoteDocumentGenerator
{
    /// <summary>Renders the customer quote PDF bytes.</summary>
    byte[] Generate(Quote quote, string vatLabel, PdfRenderContext context);
}
