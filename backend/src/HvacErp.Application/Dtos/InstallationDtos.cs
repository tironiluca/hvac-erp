using System.Text.Json;

namespace HvacErp.Application.Dtos;

/// <summary>Installer id + name on a scheduled installation.</summary>
public record InstallerSummaryDto(int Id, string Name);

/// <summary>A user currently viewing the shared installation calendar (SignalR presence roster).</summary>
public record CalendarPresenceDto(string Id, string Name);

/// <summary>A scheduled installation as shown on the calendar; <c>RowVersion</c> is the optimistic-concurrency token.</summary>
public record ScheduledInstallationDto(
    int Id,
    // The store the installation belongs to (its quote's store).
    int StoreId,
    int QuoteId,
    string QuoteNumber,
    string ClientName,
    string? City,
    DateTime Date,
    string Slot,
    string? Notes,
    // Whether a work-order PDF has been generated; its link comes from GET /api/installations/{id}/pdf-url.
    bool HasReportPdf,
    string RowVersion,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    List<InstallerSummaryDto> Installers,
    // Free-hand floor-plan sketch (vector JSON). Null when never drawn.
    JsonElement? FloorPlanSketch);

/// <summary>Create / update payload for a scheduled installation.</summary>
public record UpsertScheduledInstallationRequest(
    int QuoteId,
    DateTime Date,
    string Slot,
    string? Notes,
    List<int> InstallerIds,
    JsonElement? FloorPlanSketch = null);

/// <summary>
/// Lightweight drag-and-drop reschedule from the shared calendar — moves date / time band /
/// crew only, never the linked quote. <see cref="RowVersion"/> is the <c>ConcurrencyToken</c>
/// the caller last saw; a mismatch is a 409.
/// </summary>
public record RescheduleInstallationRequest(
    DateTime Date,
    string Slot,
    List<int> InstallerIds,
    string? RowVersion);

/// <summary>Public URL of a generated installation work order.</summary>
public record InstallationPdfResponse(string Url);
