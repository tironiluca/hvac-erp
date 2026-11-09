using System.Text.Json;
using System.Text.Json.Serialization;

namespace HvacErp.Application.Erp;

/// <summary>
/// The order document sent to the main ERP for a confirmed quote: ERP-neutral and versioned
/// (<see cref="SchemaVersion"/>). A connector maps it to its ERP's own API; the domain never sees ERP
/// specifics. <see cref="ExternalReference"/> identifies the quote so the ERP can deduplicate a
/// retried push.
/// </summary>
public sealed record ErpOrder(
    int SchemaVersion,
    string ExternalReference,
    string QuoteNumber,
    int QuoteId,
    DateTime QuoteDate,
    DateTime? ConfirmedAt,
    string? Agent,
    string Department,
    string Currency,
    ErpVat Vat,
    ErpCustomer Customer,
    ErpAddress? InstallationSite,
    IReadOnlyList<ErpOrderLine> Lines,
    ErpTotals Totals,
    string? Notes)
{
    /// <summary>Current document version.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>JSON options of the wire format (camelCase, nulls kept for a stable shape).</summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Serializes the order in the wire format.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Parses an order from the wire format.</summary>
    public static ErpOrder FromJson(string json) =>
        JsonSerializer.Deserialize<ErpOrder>(json, JsonOptions)
        ?? throw new JsonException("Empty ERP order document.");
}

/// <summary>The VAT option the quote was priced with.</summary>
public sealed record ErpVat(string Code, string? Label);

/// <summary>The customer as known to this application.</summary>
public sealed record ErpCustomer(
    string Code,
    string Name,
    string? TaxCode,
    ErpAddress? Address,
    string? Phone,
    string? Mobile,
    string? Email);

/// <summary>A postal address.</summary>
public sealed record ErpAddress(string? Street, string? City, string? PostalCode, string? Province);

/// <summary>
/// One priced line. <see cref="Kind"/> is one of <see cref="ErpLineKinds"/>; <see cref="Code"/> is the
/// item's stable code where it has one (service-item / extra / incentive codes, product EAN).
/// </summary>
public sealed record ErpOrderLine(
    int LineNumber,
    string Kind,
    string? Code,
    string Description,
    decimal Quantity,
    decimal UnitListPrice,
    decimal UnitNetPrice,
    decimal Amount);

/// <summary>Kinds of <see cref="ErpOrderLine"/>.</summary>
public static class ErpLineKinds
{
    /// <summary>An outdoor or indoor unit.</summary>
    public const string Product = "Product";

    /// <summary>A labour / material service item.</summary>
    public const string Service = "Service";

    /// <summary>An extra-work item.</summary>
    public const string Extra = "Extra";

    /// <summary>An incentive, fee or cover amount (e.g. the extended warranty).</summary>
    public const string Incentive = "Incentive";
}

/// <summary>The quote's priced totals, as on the quote (VAT included where the option includes it).</summary>
public sealed record ErpTotals(
    decimal MachinesPrice,
    decimal InstallationPrice,
    decimal ManualDiscount,
    decimal Rounding,
    decimal FinalPrice,
    decimal? InvoiceDiscountTotal,
    decimal? CustomerPays);
