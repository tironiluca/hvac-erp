namespace HvacErp.Application.Interfaces;

/// <summary>Generates the field work-order PDF for a scheduled installation, uploads it to Storage, and stamps ScheduledInstallation.ReportPdfUrl.</summary>
public interface IInstallationPdfService
{
    /// <summary>Renders the installation work order with the active template, then uploads it to Storage.</summary>
    /// <returns>The public URL of the generated PDF.</returns>
    Task<string> GenerateAndStoreAsync(int installationId, CancellationToken cancellationToken = default);
}
