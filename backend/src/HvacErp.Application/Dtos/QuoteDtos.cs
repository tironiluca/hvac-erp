using System.Text.Json;
using HvacErp.Domain.Enums;

namespace HvacErp.Application.Dtos;

/// <summary>A quote row in the list view.</summary>
public record QuoteListItemDto(
    int Id,
    int Number,
    int Version,
    int Revision,
    int ClientId,
    string ClientName,
    string ClientCode,
    string Department,
    DateTime CreatedAt,
    string CurrentStatus,
    decimal FinalPrice);

/// <summary>The full quote as shown in the wizard, including derived lifecycle flags.</summary>
public record QuoteDetailDto(
    int Id,
    int Number,
    int Version,
    int Revision,
    int? ParentQuoteId,
    int ClientId,
    string ClientName,
    int? OperatorId,
    string? OperatorName,
    string Department,
    DateTime CreatedAt,
    string CurrentStatus,
    // Derived from StatusEvents for convenience — the event log is authoritative.
    bool SurveyDone,
    DateTime? SurveyDate,
    bool Recalled,
    DateTime? RecalledDate,
    string? RecallNotes,
    bool Confirmed,
    DateTime? ConfirmedDate,
    bool Predisposed,
    string Vat,
    decimal ManualDiscount,
    decimal MachinesPrice,
    decimal InstallationPrice,
    decimal Rounding,
    decimal FinalPrice,
    List<QuoteIncentiveDto> Incentives,
    string? InstallationAddress,
    string? InstallationCity,
    string? InstallationPositionNotes,
    string? FloorPlanImageUrl,
    string? PdfUrl,
    List<QuoteProductDto> Products,
    List<DuctedUnitDto> DuctedUnits,
    List<QuoteServiceItemDto> ServiceItems,
    List<QuoteExtraDto> Extras,
    List<QuoteStatusEventDto> StatusEvents,
    List<QuoteAttachmentDto> Attachments,
    // Free-hand floor-plan sketch (vector JSON). Null when never drawn.
    JsonElement? FloorPlanSketch);

/// <summary>An external / internal unit line on a quote, with list and net price.</summary>
public record QuoteProductDto(
    int Id,
    int ProductId,
    string Brand,
    string Model,
    ProductRole Role,
    int Quantity,
    decimal Price,
    decimal DiscountedPrice);

/// <summary>
/// An incentive on a quote. For a <c>QuoteAmount</c> scheme, <see cref="Mode"/> / <see cref="Value"/>
/// are what staff chose (<c>Fixed</c> € or <c>Percent</c> of the equipment list total) and
/// <see cref="Amount"/> the result; for an invoice discount, <see cref="Amount"/> is the grossed-up
/// total and <see cref="CustomerAmount"/> what the customer pays.
/// </summary>
public record QuoteIncentiveDto(
    int IncentiveSchemeId,
    string Code,
    string Label,
    string Kind,
    string? Mode,
    decimal? Value,
    decimal? Amount,
    decimal? CustomerAmount,
    bool RequiresConfirmation,
    bool Confirmed);



/// <summary>A labour / consumable line on a quote.</summary>
public record QuoteServiceItemDto(
    int Id,
    int ServiceItemId,
    string ServiceItemName,
    decimal Quantity,
    decimal UnitPrice);

/// <summary>An extra-work line on a quote.</summary>
public record QuoteExtraDto(
    int Id,
    int ExtraTypeId,
    string ExtraTypeCode,
    string ExtraTypeLabel,
    bool QuantityBased,
    decimal Quantity,
    decimal UnitPrice);

/// <summary>One entry of a quote's lifecycle history.</summary>
public record QuoteStatusEventDto(
    int Id,
    string Status,
    DateTime OccurredAt,
    string? Notes,
    string? ChangedBy);

/// <summary>A file attached to a quote.</summary>
public record QuoteAttachmentDto(int Id, string FileName, string FileUrl, DateTime UploadedAt);

/// <summary>One ducted indoor unit (type + vent count).</summary>
public record DuctedUnitOutletDto(int Id, int Position, string SystemType, int VentCount);

/// <summary>A quote's ducted-system configuration and its outlets.</summary>
public record DuctedUnitDto(
    int Id,
    int CloudQuantity,
    int PlenumQuantity,
    bool HasFlexibleHose,
    int FlexibleHoseQuantity,
    bool HasRigidHose,
    int RigidHoseQuantity,
    int RemoteQuantity,
    List<DuctedUnitOutletDto> Outlets);

/// <summary>Product a client is buying as part of a quote. Price fields are always computed server-side from the catalog — never trust client-supplied prices.</summary>
public record QuoteProductInput(int ProductId, ProductRole Role, int Quantity);

/// <summary>
/// An incentive sent when saving a quote. <see cref="Mode"/> (<c>Fixed</c> € or <c>Percent</c> of the
/// equipment list total) and <see cref="Value"/> apply to <c>QuoteAmount</c> schemes; omitted, they
/// take the scheme's default. Computed schemes ignore them.
/// </summary>
public record QuoteIncentiveInput(int IncentiveSchemeId, string? Mode = null, decimal? Value = null);

/// <summary>One ducted indoor unit sent when saving a quote.</summary>
public record DuctedUnitOutletInput(int Position, string SystemType, int VentCount);

/// <summary>Ducted-system configuration sent when saving a quote.</summary>
public record DuctedUnitInput(
    int CloudQuantity,
    int PlenumQuantity,
    bool HasFlexibleHose,
    int FlexibleHoseQuantity,
    bool HasRigidHose,
    int RigidHoseQuantity,
    int RemoteQuantity,
    List<DuctedUnitOutletInput> Outlets);

/// <summary>A labour / consumable line sent when saving a quote.</summary>
public record QuoteServiceItemInput(int ServiceItemId, decimal Quantity);

/// <summary>One extra-work line on a quote. Price is resolved server-side from the <see cref="Domain.Entities.ExtraType"/>.</summary>
public record QuoteExtraInput(int ExtraTypeId, decimal Quantity);

/// <summary>Create / update payload for a quote. Prices are computed server-side, never taken from the client.</summary>
public record UpsertQuoteRequest(
    int ClientId,
    int? OperatorId,
    int? ParentQuoteId,
    string Department,
    bool Predisposed,
    string Vat,
    decimal ManualDiscount,
    string? InstallationAddress,
    string? InstallationCity,
    string? InstallationPositionNotes,
    List<QuoteProductInput> Products,
    List<DuctedUnitInput> DuctedUnits,
    List<QuoteServiceItemInput> ServiceItems,
    List<QuoteExtraInput> Extras,
    JsonElement? FloorPlanSketch = null,
    List<QuoteIncentiveInput>? Incentives = null);

/// <summary>Notes for a quote recall.</summary>
public record RecallQuoteRequest(string? Notes);

/// <summary>Public URL of a generated quote PDF.</summary>
public record QuotePdfResponse(string Url);
