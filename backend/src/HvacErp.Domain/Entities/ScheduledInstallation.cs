using HvacErp.Domain.Enums;

namespace HvacErp.Domain.Entities;

/// <summary>A confirmed quote scheduled for on-site installation.</summary>
public class ScheduledInstallation
{
    public int Id { get; set; }

    public int QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;

    public DateTime Date { get; set; }

    /// <summary><see cref="Entities.DaySlot"/> lookup code (<see cref="DaySlotCodes"/>).</summary>
    public string Slot { get; set; } = DaySlotCodes.Morning;
    public string? Notes { get; set; }
    public string? ReportPdfUrl { get; set; }

    /// <summary>Free-hand floor-plan sketch as vector JSON (see <see cref="Quote.FloorPlanSketchJson"/>); <c>jsonb</c>. Null when never drawn.</summary>
    public string? FloorPlanSketchJson { get; set; }

    /// <summary>
    /// Optimistic-concurrency token, regenerated on every write. The shared calendar echoes the
    /// value it last saw when rescheduling; a mismatch (someone else moved the appointment first)
    /// surfaces as a 409 so the client can refresh rather than silently clobber the other edit.
    /// </summary>
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();

    /// <summary>When the appointment was last moved/edited, and by whom (Supabase email / user id).</summary>
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public ICollection<InstallationInstaller> Installers { get; set; } = new List<InstallationInstaller>();
}

/// <summary>Crew assignment join — an installer working a given scheduled installation.</summary>
public class InstallationInstaller
{
    public int ScheduledInstallationId { get; set; }
    public ScheduledInstallation ScheduledInstallation { get; set; } = null!;

    public int InstallerId { get; set; }
    public Installer Installer { get; set; } = null!;
}
