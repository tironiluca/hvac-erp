namespace HvacErp.Application.Interfaces;

/// <summary>Generates the customer-facing quote PDF, uploads it to Storage, and stamps Quote.PdfUrl.</summary>
public interface IQuotePdfService
{
    /// <summary>Renders the customer quote PDF and saves its URL on the quote with the active template, then uploads it to Storage.</summary>
    /// <returns>The public URL of the generated PDF.</returns>
    Task<string> GenerateAndStoreAsync(int quoteId, CancellationToken cancellationToken = default);
}
