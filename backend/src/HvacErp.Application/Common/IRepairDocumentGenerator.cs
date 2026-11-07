using HvacErp.Domain.Entities;

namespace HvacErp.Application.Common;

/// <summary>
/// Renders a field work-order PDF for a repair or revision. The caller must pass a
/// <see cref="Repair"/> with Client, Quote, ServiceItems (+ServiceItem), and ScheduleEntries
/// (+Installer) eager-loaded, plus a <see cref="PdfRenderContext"/> holding the active template
/// + branding.
/// </summary>
public interface IRepairDocumentGenerator
{
    /// <summary>Renders the repair work-order PDF bytes.</summary>
    byte[] Generate(Repair repair, PdfRenderContext context);
}
