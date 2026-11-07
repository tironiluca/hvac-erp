namespace HvacErp.Application.Interfaces;

/// <summary>Generates the field work-order PDF for a repair/revision, uploads it to Storage, and stamps Repair.PdfUrl.</summary>
public interface IRepairPdfService
{
    /// <summary>Renders the repair work order with the active template, then uploads it to Storage.</summary>
    /// <returns>The public URL of the generated PDF.</returns>
    Task<string> GenerateAndStoreAsync(int repairId, CancellationToken cancellationToken = default);
}
