using HvacErp.Domain.Enums;

namespace HvacErp.Domain.Entities;

/// <summary>A repair or a periodic revision/service ("riparazione"/"revisione") — the legacy app stored both in one table distinguished by an id prefix.</summary>
public class Repair
{
    public int Id { get; set; }

    public RepairType Type { get; set; }

    /// <summary><see cref="Entities.RepairStatus"/> lookup code (<see cref="RepairStatusCodes"/>); history in <see cref="StatusEvents"/>.</summary>
    public string Status { get; set; } = RepairStatusCodes.New;

    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;

    /// <summary>The quote/installation this repair or revision originated from, if any.</summary>
    public int? QuoteId { get; set; }
    public Quote? Quote { get; set; }

    public string? Brand { get; set; }
    public string Description { get; set; } = string.Empty;

    public bool Urgent { get; set; }
    public bool Predisposed { get; set; }
    public bool AllInclusive { get; set; }

    public int UnitCount { get; set; }
    public int SplitCount { get; set; }

    public decimal Rounding { get; set; }

    public DateTime InsertedAt { get; set; } = DateTime.UtcNow;
    public DateTime? InstallationDate { get; set; }

    /// <summary>Free-hand floor-plan sketch as vector JSON (see <see cref="Quote.FloorPlanSketchJson"/>); <c>jsonb</c>. Null when never drawn.</summary>
    public string? FloorPlanSketchJson { get; set; }

    public string? PdfUrl { get; set; }

    public ICollection<RepairServiceItem> ServiceItems { get; set; } = new List<RepairServiceItem>();
    public ICollection<RepairScheduleEntry> ScheduleEntries { get; set; } = new List<RepairScheduleEntry>();
    public ICollection<RepairStatusEvent> StatusEvents { get; set; } = new List<RepairStatusEvent>();
    public ICollection<FGasRecord> FGasRecords { get; set; } = new List<FGasRecord>();
}

/// <summary>An itemized labor/consumable line on a repair (from the <see cref="ServiceItem"/> price list).</summary>
public class RepairServiceItem
{
    public int Id { get; set; }

    public int RepairId { get; set; }
    public Repair Repair { get; set; } = null!;

    public int ServiceItemId { get; set; }
    public ServiceItem ServiceItem { get; set; } = null!;

    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}

/// <summary>A scheduled visit/appointment for a repair, optionally assigned to an installer.</summary>
public class RepairScheduleEntry
{
    public int Id { get; set; }

    public int RepairId { get; set; }
    public Repair Repair { get; set; } = null!;

    public int? InstallerId { get; set; }
    public Installer? Installer { get; set; }

    public DateTime Date { get; set; }
    public string? Notes { get; set; }
}
