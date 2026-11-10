namespace HvacErp.Application.Interfaces;

/// <summary>Generates the customer-facing quote PDF, uploads it to Storage, and records its storage path on the quote.</summary>
public interface IQuotePdfService
{
    /// <summary>Renders the customer quote PDF with the active template, then uploads it to Storage.</summary>
    /// <returns>A short-lived link to the generated PDF.</returns>
    Task<string> GenerateAndStoreAsync(int quoteId, CancellationToken cancellationToken = default);

    /// <summary>A short-lived link to the PDF generated last. 404 when the record does not exist for the caller or has no PDF yet.</summary>
    Task<string> GetUrlAsync(int quoteId, CancellationToken cancellationToken = default);
}
