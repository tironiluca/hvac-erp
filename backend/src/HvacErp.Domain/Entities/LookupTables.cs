namespace HvacErp.Domain.Entities;

/// <summary>
/// Base shape shared by every enum-replacement lookup table: a stable <see cref="Code"/> primary key
/// (matches the value stored on the referencing column and sent over the wire) plus presentation
/// metadata the app and UI read as data rather than baking into a <c>switch</c>.
/// </summary>
public abstract class LookupBase
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Optional hex colour (e.g. <c>#2E7D32</c>) for status chips / grid rows.</summary>
    public string? Color { get; set; }

    public int SortOrder { get; set; }
    public bool Active { get; set; } = true;
}

/// <summary>
/// VAT scheme applied to a quote. The multipliers are exactly what
/// <c>QuotePricingService</c> multiplies by, so a tax-law change is a row edit — no re-derivation.
/// </summary>
public class VatOption : LookupBase
{
    /// <summary>One of <see cref="Enums.VatBlendShapes"/> — selects the blend formula shape.</summary>
    public string BlendShape { get; set; } = string.Empty;

    /// <summary>Gross-up factor for the machine cost (legacy <c>1.22</c> for Standard22 / <c>1.10</c> for Reduced10).</summary>
    public decimal MachinesMultiplier { get; set; } = 1m;

    /// <summary>Gross-up factor for the installation labour under <see cref="Enums.VatBlendShapes.Split"/> (legacy <c>2.2</c>).</summary>
    public decimal InstallationMultiplier { get; set; } = 1m;

    /// <summary>Line shown on the customer PDF (e.g. <c>IVA Compresa (22%)</c>).</summary>
    public string PdfLabel { get; set; } = string.Empty;
}

/// <summary>A repair/revision state. The 10 rows sit at their legacy <c>RipRev.Stato</c> ordinals.</summary>
public class RepairStatus : LookupBase
{
    /// <summary>Legacy <c>RipRev.Stato</c> value 0–9 this state maps to.</summary>
    public int LegacyOrdinal { get; set; }

    /// <summary>No further transitions expected (Closed / ClosedByInstallers).</summary>
    public bool IsTerminal { get; set; }

    /// <summary>The customer needs direct after-sales assistance (AssistanceRequested / AssistanceContacted).</summary>
    public bool RequiresDirectAssistance { get; set; }
}

/// <summary>A quote lifecycle milestone (mirrors the <see cref="QuoteStatusEvent"/> log's values).</summary>
public class QuoteStatus : LookupBase
{
    public bool IsTerminal { get; set; }
}

/// <summary>Ducted ("canalizzato") indoor-unit product line: Standard or Airzone zoning dampers.</summary>
public class DuctSystemType : LookupBase
{
}

/// <summary>Which desk a quote originates from (Store counter vs. Field agent).</summary>
public class QuoteDepartment : LookupBase
{
}

/// <summary>Installation appointment time band (Morning / Afternoon / FullDay).</summary>
public class DaySlot : LookupBase
{
}

/// <summary>
/// One append-only entry in a repair's status history — mirrors <see cref="QuoteStatusEvent"/>.
/// <c>RepairService.ChangeStatusAsync</c> writes one row per transition.
/// </summary>
public class RepairStatusEvent : ITenantOwned
{
    public int TenantId { get; set; }

    public int Id { get; set; }

    public int RepairId { get; set; }
    public Repair Repair { get; set; } = null!;

    /// <summary>The <see cref="RepairStatus"/> code moved to.</summary>
    public string Status { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    /// <summary>Supabase user id / display name of whoever triggered the transition, when known.</summary>
    public string? ChangedBy { get; set; }
}
