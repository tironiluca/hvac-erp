using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Dtos;
using HvacErp.Application.Interfaces;
using HvacErp.Domain.Entities;

namespace HvacErp.Application.Services;

/// <summary>Default <see cref="Interfaces.IQuoteAttachmentService"/> over Supabase Storage.</summary>
public class QuoteAttachmentService(IAppDbContext context, IFileStorageService storage) : IQuoteAttachmentService
{
    public async Task<QuoteAttachmentDto> UploadAsync(int quoteId, string fileName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        _ = await context.Quotes.FindAsync([quoteId], cancellationToken)
            ?? throw new NotFoundException(nameof(Quote), quoteId);

        var path = $"quotes/{quoteId}/attachments/{Guid.NewGuid():N}-{SanitizeFileName(fileName)}";
        var url = await storage.UploadAsync(path, content, contentType, cancellationToken);

        var attachment = new QuoteAttachment
        {
            QuoteId = quoteId,
            FileName = fileName,
            FileUrl = url
        };
        context.QuoteAttachments.Add(attachment);
        await context.SaveChangesAsync(cancellationToken);

        return new QuoteAttachmentDto(attachment.Id, attachment.FileName, attachment.FileUrl, attachment.UploadedAt);
    }

    public async Task DeleteAsync(int quoteId, int attachmentId, CancellationToken cancellationToken = default)
    {
        // Child rows are filtered by tenant only; going through the quote applies its store filter too.
        var attachment = await context.QuoteAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.QuoteId == quoteId && a.Quote.Id == quoteId, cancellationToken)
            ?? throw new NotFoundException(nameof(QuoteAttachment), attachmentId);

        // Removes the DB row only — the underlying Storage object is left in place. Not worth
        // the coupling (parsing the stored path back out of the public URL) for an MVP; orphaned
        // files can be swept later if storage usage becomes a concern.
        context.QuoteAttachments.Remove(attachment);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static string SanitizeFileName(string fileName) =>
        string.Concat(fileName.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '-'));
}
