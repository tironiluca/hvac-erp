namespace HvacErp.Domain.Enums;

// Stable string keys for the DB-backed lookup tables (Domain/Entities/LookupTables.cs). Code that has
// genuine logic — VAT blend, unit-compatibility, workflow guards — branches on these constants; the
// value is the lookup row's Code (and the column value, and the JSON wire value — all identical).

/// <summary>Codes of the <see cref="Entities.VatOption"/> lookup.</summary>
public static class VatCodes
{
    public const string Standard22 = "Standard22";
    public const string Reduced10 = "Reduced10";
    public const string Exempt = "Exempt";
}

/// <summary>How <c>QuotePricingService</c> blends VAT into the final price for a given VAT option.</summary>
public static class VatBlendShapes
{
    /// <summary>Legacy "IVA compresa": installation labour and the machine remainder are grossed separately.</summary>
    public const string Split = "Split";

    /// <summary>Legacy "IVA agevolata": one multiplier over the whole (machines + installation) total.</summary>
    public const string Flat = "Flat";

    /// <summary>VAT-excluded: the total passes through untouched.</summary>
    public const string None = "None";
}

/// <summary>Codes of the <see cref="Entities.QuoteDepartment"/> lookup.</summary>
public static class QuoteDepartmentCodes
{
    public const string Store = "Store";
    public const string Field = "Field";
}

/// <summary>Codes of the <see cref="Entities.QuoteStatus"/> lookup.</summary>
public static class QuoteStatusCodes
{
    public const string Draft = "Draft";
    public const string SurveyDone = "SurveyDone";
    public const string Recalled = "Recalled";
    public const string Confirmed = "Confirmed";
}

/// <summary>Codes of the <see cref="Entities.DuctSystemType"/> lookup.</summary>
public static class DuctSystemTypeCodes
{
    public const string Standard = "Standard";
    public const string Airzone = "Airzone";
}

/// <summary>Codes of the <see cref="Entities.DaySlot"/> lookup.</summary>
public static class DaySlotCodes
{
    public const string Morning = "Morning";
    public const string Afternoon = "Afternoon";
    public const string FullDay = "FullDay";
}

/// <summary>
/// The 10 repair states confirmed in Phase 8a, positioned at their legacy <c>RipRev.Stato</c>
/// ordinals (0–9). See <c>repair_statuses</c> for labels/colours/workflow flags.
/// </summary>
public static class RepairStatusCodes
{
    public const string New = "New";                              // 0
    public const string Urgent = "Urgent";                        // 1
    public const string Scheduled = "Scheduled";                  // 2 — assigned/handled, work not started
    public const string InProgress = "InProgress";                // 3
    public const string AwaitingPayment = "AwaitingPayment";      // 4
    public const string PendingOffSeason = "PendingOffSeason";    // 5
    public const string ClosedByInstallers = "ClosedByInstallers";// 6
    public const string Closed = "Closed";                        // 7
    public const string AssistanceRequested = "AssistanceRequested";  // 8
    public const string AssistanceContacted = "AssistanceContacted";  // 9
}
