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

        // What the file is comes from its bytes; the content type the client sent is ignored.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var header = buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, UploadedFileInspector.HeaderLength));
        var detectedType = UploadedFileInspector.DetectAttachmentType(header)
            ?? throw new BusinessRuleException($"Only {UploadedFileInspector.AllowedAttachmentsDescription} can be attached.");

        buffer.Position = 0;
        var path = $"quotes/{quoteId}/attachments/{Guid.NewGuid():N}-{SanitizeFileName(fileName)}";
        var attachment = new QuoteAttachment
        {
            QuoteId = quoteId,
            FileName = fileName,
            FilePath = await storage.UploadAsync(path, buffer, detectedType, cancellationToken)
        };
        context.QuoteAttachments.Add(attachment);
        await context.SaveChangesAsync(cancellationToken);

        return new QuoteAttachmentDto(attachment.Id, attachment.FileName, attachment.UploadedAt);
    }

    public async Task<string> GetUrlAsync(int quoteId, int attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await FindAsync(quoteId, attachmentId, cancellationToken);
        return await storage.CreateSignedUrlAsync(attachment.FilePath, DocumentLinks.Lifetime, cancellationToken);
    }

    public async Task DeleteAsync(int quoteId, int attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await FindAsync(quoteId, attachmentId, cancellationToken);

        context.QuoteAttachments.Remove(attachment);
        await context.SaveChangesAsync(cancellationToken);

        // The row is gone first: a file left behind is clutter, a row pointing at a deleted file is a
        // broken link. Best effort — a storage hiccup does not fail the request.
        await storage.TryDeleteAsync(attachment.FilePath, cancellationToken);
    }

    // Child rows are filtered by tenant only; going through the quote applies its store filter too.
    private async Task<QuoteAttachment> FindAsync(int quoteId, int attachmentId, CancellationToken cancellationToken) =>
        await context.QuoteAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.QuoteId == quoteId && a.Quote.Id == quoteId, cancellationToken)
            ?? throw new NotFoundException(nameof(QuoteAttachment), attachmentId);

    private static string SanitizeFileName(string fileName) =>
        string.Concat(fileName.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '-'));
}
