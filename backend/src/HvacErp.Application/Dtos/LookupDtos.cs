namespace HvacErp.Application.Dtos;

/// <summary>An installer and their crew.</summary>
public record InstallerDto(int Id, string Name, string? Mobile, string? Email, string? Department, int? Crew);

/// <summary>A sales operator / agent.</summary>
public record OperatorDto(int Id, string Code, string Name, int? Extension, string? Email);

/// <summary>A client marketing source.</summary>
public record ClientSourceDto(int Id, string Description);

/// <summary>A product type.</summary>
public record ProductTypeDto(int Id, string Name, string? Description);

/// <summary>A labour / consumables price-list item.</summary>
public record ServiceItemDto(int Id, string? Code, string Name, string? Description, decimal Price, int? ProductTypeId);

/// <summary>An extra-work line type with its default price and pricing flags.</summary>
public record ExtraTypeDto(
    int Id,
    string Code,
    string Label,
    string? Description,
    decimal DefaultPrice,
    bool QuantityBased,
    bool ExcludedFromVat,
    int SortOrder);

/// <summary>
/// An incentive scheme with its calculation (<see cref="Domain.Entities.IncentiveScheme"/>). The quote
/// wizard offers quote-level schemes on the quote and <c>LineAmount</c> schemes on each product line,
/// and its live price preview mirrors the server calculation from these parameters.
/// </summary>
public record IncentiveSchemeDto(
    int Id,
    string Code,
    string Label,
    string? Description,
    string Kind,
    decimal? DefaultAmount,
    decimal? Percent,
    bool AddToPrice,
    bool IncludeInInvoiceDiscountBasis,
    bool RequiresVat,
    bool RequiresConfirmation,
    decimal? Threshold,
    decimal? RateBelowThreshold,
    decimal? RateAboveThreshold,
    decimal? RoundingStep,
    int SortOrder);

/// <summary>A row from one of the Phase 8c enum-replacement lookup tables.</summary>
public record LookupItemDto(string Code, string Label, string? Description, string? Color, int SortOrder);

/// <summary>A VAT scheme with the blend shape and multipliers the pricing engine applies.</summary>
public record VatOptionDto(
    string Code,
    string Label,
    string? Color,
    int SortOrder,
    string BlendShape,
    decimal MachinesMultiplier,
    decimal InstallationMultiplier,
    string PdfLabel);

/// <summary>A repair state with its workflow flags.</summary>
public record RepairStatusDto(
    string Code,
    string Label,
    string? Description,
    string? Color,
    int SortOrder,
    int LegacyOrdinal,
    bool IsTerminal,
    bool RequiresDirectAssistance);

/// <summary>A product price list.</summary>
public record ProductPriceListDto(int Id, string Description, decimal Vat, decimal Markup, bool CoversExternalCode);

/// <summary>One rung of a price list's cascading discount ladder (<see cref="Domain.Entities.PriceListDiscountTier"/>).</summary>
public record DiscountTierDto(int TierOrder, decimal Percentage);

/// <summary>An HVAC unit with its price variants and discount ladder.</summary>
public record ProductDto(
    int Id,
    int PriceListId,
    string Brand,
    string Model,
    string? Category,
    string? Ean,
    decimal? MaxBtu,
    decimal? Power,
    decimal Price,
    decimal? HydronicPrice,
    bool IsHydronic,
    decimal? PriceNotPrepared,
    decimal? PricePrepared,
    string? RefrigerantGas,
    bool Wifi,
    bool Active,
    int Stock,
    IReadOnlyList<DiscountTierDto> DiscountTiers);
