using HvacErp.Domain.Entities;

namespace HvacErp.Application.Common;

/// <summary>
/// Renders a field work-order PDF for a scheduled installation. The caller must pass a
/// <see cref="ScheduledInstallation"/> with Quote (+Client), and Installers (+Installer)
/// eager-loaded, the resolved time-slot label (from the <c>day_slots</c> lookup), and a
/// <see cref="PdfRenderContext"/> holding the active template + branding.
/// </summary>
public interface IInstallationDocumentGenerator
{
    /// <summary>Renders the installation work-order PDF bytes.</summary>
    byte[] Generate(ScheduledInstallation installation, string slotLabel, PdfRenderContext context);
}
