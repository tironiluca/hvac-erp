namespace HvacErp.Application.Interfaces;

/// <summary>Generates the field work-order PDF for a scheduled installation, uploads it to Storage, and records its storage path on the installation.</summary>
public interface IInstallationPdfService
{
    /// <summary>Renders the installation work order with the active template, then uploads it to Storage.</summary>
    /// <returns>A short-lived link to the generated PDF.</returns>
    Task<string> GenerateAndStoreAsync(int installationId, CancellationToken cancellationToken = default);

    /// <summary>A short-lived link to the PDF generated last. 404 when the record does not exist for the caller or has no PDF yet.</summary>
    Task<string> GetUrlAsync(int installationId, CancellationToken cancellationToken = default);
}
