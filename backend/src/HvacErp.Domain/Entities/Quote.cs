using HvacErp.Domain.Enums;

namespace HvacErp.Domain.Entities;

/// <summary>
/// A "preventivo" — an HVAC installation quote. Numbering is a real surrogate id
/// (<see cref="Id"/>) plus a human-facing Number/Version/Revision triplet, replacing
/// the legacy app's single 8-character string encoding all three.
/// </summary>
public class Quote
{
    public int Id { get; set; }

    public int Number { get; set; }
    public int Version { get; set; } = 1;
    public int Revision { get; set; } = 0;

    /// <summary>Set when this quote is a new version/revision of an earlier one for the same client.</summary>
    public int? ParentQuoteId { get; set; }
    public Quote? ParentQuote { get; set; }

    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public int? OperatorId { get; set; }
    public Operator? Operator { get; set; }

    /// <summary><see cref="QuoteDepartment"/> lookup code (<see cref="QuoteDepartmentCodes"/>).</summary>
    public string Department { get; set; } = QuoteDepartmentCodes.Store;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Denormalized "furthest milestone reached" for cheap list-view filtering; the authoritative
    /// history is <see cref="StatusEvents"/>. Kept in sync by the workflow transition methods.
    /// A <see cref="QuoteStatus"/> lookup code (<see cref="QuoteStatusCodes"/>).
    /// </summary>
    public string CurrentStatus { get; set; } = QuoteStatusCodes.Draft;

    /// <summary>Whether the site is already pre-wired/pre-plumbed for the unit — drives which unit price (prepared/not) applies.</summary>
    public bool Predisposed { get; set; }

    /// <summary><see cref="Entities.VatOption"/> lookup code (<see cref="VatCodes"/>).</summary>
    public string Vat { get; set; } = VatCodes.Standard22;
    public decimal ManualDiscount { get; set; }

    public decimal MachinesPrice { get; set; }
    public decimal InstallationPrice { get; set; }
    public decimal Rounding { get; set; }
    public decimal FinalPrice { get; set; }


    /// <summary>
    /// Optional per-quote installation site (second homes, rentals, "cliente amico" referral installs).
    /// The UI and PDF fall back to the <see cref="Client"/>'s address when null; a <see cref="Repair"/>
    /// created from this quote resolves its execution address the same way.
    /// </summary>
    public string? InstallationAddress { get; set; }
    public string? InstallationCity { get; set; }

    public string? InstallationPositionNotes { get; set; }
    public string? FloorPlanImageUrl { get; set; }

    /// <summary>
    /// Free-hand floor-plan sketch as vector JSON (<c>{ version, width, height, strokes[] }</c>),
    /// stored in a <c>jsonb</c> column. Replaces the legacy Windows-Ink <c>disPlan</c> form; drawn
    /// into the quote PDF as SVG and reopened for editing in the quote form. Null when never drawn.
    /// </summary>
    public string? FloorPlanSketchJson { get; set; }

    public string? PdfUrl { get; set; }

    public ICollection<QuoteProduct> Products { get; set; } = new List<QuoteProduct>();
    public ICollection<DuctedUnit> DuctedUnits { get; set; } = new List<DuctedUnit>();
    public ICollection<QuoteServiceItem> ServiceItems { get; set; } = new List<QuoteServiceItem>();
    public ICollection<QuoteExtra> Extras { get; set; } = new List<QuoteExtra>();

    /// <summary>Tax incentives, fees and invoice discounts on the quote (see <see cref="IncentiveScheme"/>).</summary>
    public ICollection<QuoteIncentive> Incentives { get; set; } = new List<QuoteIncentive>();
    public ICollection<QuoteStatusEvent> StatusEvents { get; set; } = new List<QuoteStatusEvent>();
    public ICollection<QuoteAttachment> Attachments { get; set; } = new List<QuoteAttachment>();
    public ICollection<FGasRecord> FGasRecords { get; set; } = new List<FGasRecord>();
    public ICollection<Repair> Repairs { get; set; } = new List<Repair>();
    public ICollection<ScheduledInstallation> ScheduledInstallations { get; set; } = new List<ScheduledInstallation>();
}

/// <summary>A unit (external condenser or internal split) included in a quote.</summary>
public class QuoteProduct
{
    public int Id { get; set; }

    public int QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public ProductRole Role { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal Price { get; set; }
    public decimal DiscountedPrice { get; set; }
}

/// <summary>Ducted ("canalizzato") system configuration for one quote — the shared plenum/hose/remote
/// counts plus a row per ducted indoor unit in <see cref="Outlets"/>.</summary>
public class DuctedUnit
{
    public int Id { get; set; }

    public int QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;

    public int CloudQuantity { get; set; }
    public int PlenumQuantity { get; set; }
    public bool HasFlexibleHose { get; set; }
    public int FlexibleHoseQuantity { get; set; }
    public bool HasRigidHose { get; set; }
    public int RigidHoseQuantity { get; set; }
    public int RemoteQuantity { get; set; }

    /// <summary>One row per ducted indoor unit — replaces the legacy <c>Canalizzati.tipo1..5</c>/<c>qta1..5</c> fixed slots.</summary>
    public ICollection<DuctedUnitOutlet> Outlets { get; set; } = new List<DuctedUnitOutlet>();
}

/// <summary>
/// One ducted indoor unit on a quote's <see cref="DuctedUnit"/>. Each has its own Standard/Airzone
/// type and its own vent count, both of which feed the per-outlet ducted-system price formula
/// (legacy <c>InsPrev.vb</c> <c>calcolaPrezzo</c> loops these per <c>cmbCan{n}</c>).
/// </summary>
public class DuctedUnitOutlet
{
    public int Id { get; set; }

    public int DuctedUnitId { get; set; }
    public DuctedUnit DuctedUnit { get; set; } = null!;

    /// <summary>1-based position within the ducted system (legacy slot 1..5).</summary>
    public int Position { get; set; }

    /// <summary>Legacy <c>Canalizzati.tipo{n}</c>: 'S' = Standard, 'A' = Airzone. A <see cref="Entities.DuctSystemType"/> code (<see cref="DuctSystemTypeCodes"/>).</summary>
    public string SystemType { get; set; } = DuctSystemTypeCodes.Standard;

    /// <summary>Number of air outlets/vents on this ducted unit (legacy <c>cmbCan{n}</c> / <c>numCan</c>).</summary>
    public int VentCount { get; set; }
}

/// <summary>An itemized labor/consumable line on a quote (from the <see cref="ServiceItem"/> price list).</summary>
public class QuoteServiceItem
{
    public int Id { get; set; }

    public int QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;

    public int ServiceItemId { get; set; }
    public ServiceItem ServiceItem { get; set; } = null!;

    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}

/// <summary>
/// One "extra work" line on a quote (electrical wiring, scaffolding, extra holes, WiFi module, …).
/// Replaces the legacy <c>Preventivi</c> fixed <c>Ponteggio</c>/<c>Autoscala</c>/… bool+meters columns:
/// a new extra the business needs later is an <see cref="ExtraType"/> row, not a schema change.
/// </summary>
public class QuoteExtra
{
    public int Id { get; set; }

    public int QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;

    public int ExtraTypeId { get; set; }
    public ExtraType ExtraType { get; set; } = null!;

    /// <summary>Meters/holes for quantity-based extras; 1 for on/off extras.</summary>
    public decimal Quantity { get; set; } = 1;

    /// <summary>Snapshot of <see cref="ExtraType.DefaultPrice"/> taken when the quote was priced.</summary>
    public decimal UnitPrice { get; set; }
}

/// <summary>
/// One append-only entry in a quote's lifecycle log — replaces the legacy flattened
/// <c>SurveyDone</c>/<c>SurveyDate</c>, <c>Recalled</c>/<c>RecalledDate</c>/<c>RecallNotes</c>,
/// <c>Confirmed</c>/<c>ConfirmedDate</c> column pairs (and the legacy <c>StatoPreventivi</c>/
/// <c>DatePreventivi</c> satellite tables they descend from).
/// </summary>
public class QuoteStatusEvent
{
    public int Id { get; set; }

    public int QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;

    /// <summary>The <see cref="Entities.QuoteStatus"/> code moved to (<see cref="QuoteStatusCodes"/>).</summary>
    public string Status { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    /// <summary>Supabase user id / display name of whoever triggered the transition, when known.</summary>
    public string? ChangedBy { get; set; }
}

/// <summary>A photo or floor plan uploaded against a quote, stored in Supabase Storage.</summary>
public class QuoteAttachment
{
    public int Id { get; set; }

    public int QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
