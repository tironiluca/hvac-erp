using System.Text.Json;
using HvacErp.Domain.Enums;

namespace HvacErp.Application.Dtos;

/// <summary>A repair row in the list view.</summary>
public record RepairListItemDto(
    int Id,
    RepairType Type,
    string Status,
    int ClientId,
    string ClientName,
    string? Brand,
    string Description,
    bool Urgent,
    DateTime InsertedAt,
    decimal Total);

/// <summary>A labour / consumable line on a repair.</summary>
public record RepairServiceItemDto(int Id, int ServiceItemId, string ServiceItemName, decimal Quantity, decimal UnitPrice);

/// <summary>An appointment on a repair.</summary>
public record RepairScheduleEntryDto(int Id, int? InstallerId, string? InstallerName, DateTime Date, string? Notes);

/// <summary>One entry of a repair's status history.</summary>
public record RepairStatusEventDto(int Id, string Status, DateTime OccurredAt, string? Notes, string? ChangedBy);

/// <summary>The full repair with lines, appointments and status history.</summary>
public record RepairDetailDto(
    int Id,
    RepairType Type,
    string Status,
    int ClientId,
    string ClientName,
    int? QuoteId,
    string? Brand,
    string Description,
    bool Urgent,
    bool Predisposed,
    bool AllInclusive,
    int UnitCount,
    int SplitCount,
    decimal Rounding,
    DateTime InsertedAt,
    DateTime? InstallationDate,
    // Whether a work-order PDF has been generated; its link comes from GET /api/repairs/{id}/pdf-url.
    bool HasPdf,
    decimal Total,
    List<RepairServiceItemDto> ServiceItems,
    List<RepairScheduleEntryDto> ScheduleEntries,
    List<RepairStatusEventDto> StatusEvents,
    // Free-hand floor-plan sketch (vector JSON). Null when never drawn.
    JsonElement? FloorPlanSketch);

/// <summary>A labour / consumable line sent when saving a repair.</summary>
public record RepairServiceItemInput(int ServiceItemId, decimal Quantity);

/// <summary>Create / update payload for a repair or revision.</summary>
public record UpsertRepairRequest(
    int ClientId,
    int? QuoteId,
    RepairType Type,
    string? Brand,
    string Description,
    bool Urgent,
    bool Predisposed,
    bool AllInclusive,
    int UnitCount,
    int SplitCount,
    decimal Rounding,
    DateTime? InstallationDate,
    List<RepairServiceItemInput> ServiceItems,
    JsonElement? FloorPlanSketch = null);

/// <summary>Moves a repair to a <c>repair_statuses</c> code.</summary>
public record ChangeRepairStatusRequest(string Status);

/// <summary>Books an appointment on a repair.</summary>
public record AddRepairScheduleEntryRequest(int? InstallerId, DateTime Date, string? Notes);

/// <summary>Public URL of a generated repair work order.</summary>
public record RepairPdfResponse(string Url);
