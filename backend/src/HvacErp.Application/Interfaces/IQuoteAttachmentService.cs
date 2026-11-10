using HvacErp.Application.Dtos;

namespace HvacErp.Application.Interfaces;

/// <summary>Quote photo / document attachments in Supabase Storage.</summary>
public interface IQuoteAttachmentService
{
    /// <summary>Stores a file under the quote's folder and records it as an attachment.</summary>
    Task<QuoteAttachmentDto> UploadAsync(int quoteId, string fileName, Stream content, string contentType, CancellationToken cancellationToken = default);
    /// <summary>A short-lived link to the attachment's file.</summary>
    Task<string> GetUrlAsync(int quoteId, int attachmentId, CancellationToken cancellationToken = default);

    /// <summary>Removes an attachment row and its stored file.</summary>
    Task DeleteAsync(int quoteId, int attachmentId, CancellationToken cancellationToken = default);
}
