namespace HvacErp.Application.Dtos;

/// <summary>Everything the home dashboard shows, in one round trip.</summary>
/// <param name="WeekStart">First day of the week the <paramref name="WeekInstallations"/> cover.</param>
/// <param name="Quotes">Where open quotes sit in the sales pipeline.</param>
/// <param name="OpenRepairsByStatus">Open (non-terminal) repairs per status, in lookup order.</param>
/// <param name="UrgentOpenRepairs">Open repairs flagged urgent.</param>
/// <param name="WeekInstallations">Installations scheduled in the 7 days from <paramref name="WeekStart"/>.</param>
/// <param name="RecentActivity">Latest quote / repair status changes, newest first.</param>
public record DashboardDto(
    DateTime WeekStart,
    QuotePipelineDto Quotes,
    List<StatusCountDto> OpenRepairsByStatus,
    int UrgentOpenRepairs,
    List<DashboardInstallationDto> WeekInstallations,
    List<ActivityItemDto> RecentActivity);

/// <summary>Quote follow-up counts — each open status is the step the quote is waiting on.</summary>
/// <param name="AwaitingSurvey">Draft quotes: the site survey is next.</param>
/// <param name="AwaitingRecall">Surveyed quotes: the customer call-back is next.</param>
/// <param name="AwaitingConfirmation">Recalled quotes: the customer decision is next.</param>
/// <param name="ConfirmedLast30Days">Quotes confirmed in the last 30 days.</param>
/// <param name="OldestOpen">The longest-waiting open quotes (up to 5).</param>
public record QuotePipelineDto(
    int AwaitingSurvey,
    int AwaitingRecall,
    int AwaitingConfirmation,
    int ConfirmedLast30Days,
    List<QuoteListItemDto> OldestOpen);

/// <summary>A lookup status code with how many records currently sit in it.</summary>
public record StatusCountDto(string Status, int Count);

/// <summary>A scheduled installation row on the dashboard (no sketch / PDF payload).</summary>
public record DashboardInstallationDto(
    int Id,
    int QuoteId,
    string QuoteNumber,
    string ClientName,
    string? City,
    DateTime Date,
    string Slot,
    List<string> Installers);

/// <summary>One status change in the activity feed.</summary>
/// <param name="Kind"><c>Quote</c> or <c>Repair</c>.</param>
/// <param name="EntityId">Id of the quote / repair, for linking.</param>
/// <param name="Reference">Quote number (e.g. <c>12/3</c>) or repair id (e.g. <c>#45</c>).</param>
/// <param name="ClientName">Customer the record belongs to.</param>
/// <param name="Status">The status code moved to.</param>
/// <param name="OccurredAt">When it happened (UTC).</param>
/// <param name="ChangedBy">Who did it, when known.</param>
public record ActivityItemDto(
    string Kind,
    int EntityId,
    string Reference,
    string ClientName,
    string Status,
    DateTime OccurredAt,
    string? ChangedBy);
