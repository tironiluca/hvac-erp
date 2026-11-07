using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Interfaces;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;

namespace HvacErp.Application.Services;

/// <summary>Loads a quote, renders its customer PDF and stores it.</summary>
public class QuotePdfService(
    IAppDbContext context,
    IQuoteDocumentGenerator generator,
    IPdfTemplateService templates,
    IFileStorageService storage) : IQuotePdfService
{
    public async Task<string> GenerateAndStoreAsync(int quoteId, CancellationToken cancellationToken = default)
    {
        var quote = await context.Quotes
            .Include(q => q.Client)
            .Include(q => q.Operator)
            .Include(q => q.Products).ThenInclude(p => p.Product)
            .Include(q => q.DuctedUnits).ThenInclude(d => d.Outlets)
            .Include(q => q.ServiceItems).ThenInclude(s => s.ServiceItem)
            .Include(q => q.Extras).ThenInclude(e => e.ExtraType)
            .FirstOrDefaultAsync(q => q.Id == quoteId, cancellationToken)
            ?? throw new NotFoundException(nameof(Quote), quoteId);

        var vatLabel = await context.VatOptions
            .Where(v => v.Code == quote.Vat)
            .Select(v => v.PdfLabel)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var renderContext = await PdfRenderContextFactory.CreateAsync(
            PdfDocumentType.Quote, context, templates, storage, cancellationToken);

        var pdfBytes = generator.Generate(quote, vatLabel, renderContext);
        var fileName = $"preventivo-{quote.Number}-v{quote.Version}r{quote.Revision}.pdf";
        var path = $"quotes/{quote.Id}/{fileName}";

        using var stream = new MemoryStream(pdfBytes);
        var url = await storage.UploadAsync(path, stream, "application/pdf", cancellationToken);

        quote.PdfUrl = url;
        await context.SaveChangesAsync(cancellationToken);

        return url;
    }
}
