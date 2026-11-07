namespace HvacErp.Application.Dtos;

/// <summary>An F-Gas refrigerant-handling record.</summary>
public record FGasRecordDto(
    int Id,
    int? QuoteId,
    int? RepairId,
    string GasType,
    decimal KilogramsOfGas,
    bool IsRefill,
    bool HasExtraCharge,
    DateTime RecordedAt);

/// <summary>Create payload for an F-Gas record (exactly one of quote / repair).</summary>
public record UpsertFGasRecordRequest(
    int? QuoteId,
    int? RepairId,
    string GasType,
    decimal KilogramsOfGas,
    bool IsRefill,
    bool HasExtraCharge);
