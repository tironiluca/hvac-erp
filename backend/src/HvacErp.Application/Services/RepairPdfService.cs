using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Interfaces;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;

namespace HvacErp.Application.Services;

/// <summary>Loads a repair, renders its work order and stores it.</summary>
public class RepairPdfService(
    IAppDbContext context,
    IRepairDocumentGenerator generator,
    IPdfTemplateService templates,
    IFileStorageService storage) : IRepairPdfService
{
    public async Task<string> GenerateAndStoreAsync(int repairId, CancellationToken cancellationToken = default)
    {
        var repair = await context.Repairs
            .Include(r => r.Client)
            .Include(r => r.Quote)
            .Include(r => r.ServiceItems).ThenInclude(s => s.ServiceItem)
            .Include(r => r.ScheduleEntries).ThenInclude(s => s.Installer)
            .FirstOrDefaultAsync(r => r.Id == repairId, cancellationToken)
            ?? throw new NotFoundException(nameof(Repair), repairId);

        var renderContext = await PdfRenderContextFactory.CreateAsync(
            PdfDocumentType.Repair, context, templates, storage, cancellationToken);

        var pdfBytes = generator.Generate(repair, renderContext);
        var fileName = $"{(repair.Type == RepairType.Revision ? "revision" : "repair")}-{repair.Id}.pdf";
        var path = $"repairs/{repair.Id}/{fileName}";

        using var stream = new MemoryStream(pdfBytes);
        // A PDF generated under an older file name is replaced, not left beside the new one.
        var previous = repair.PdfPath;
        repair.PdfPath = await storage.UploadAsync(path, stream, "application/pdf", cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        if (previous is not null && previous != repair.PdfPath)
        {
            await storage.TryDeleteAsync(previous, cancellationToken);
        }

        return await storage.CreateSignedUrlAsync(repair.PdfPath, DocumentLinks.Lifetime, cancellationToken);
    }

    public async Task<string> GetUrlAsync(int repairId, CancellationToken cancellationToken = default)
    {
        // Through the filtered set: only someone who can read the record gets a link to its PDF.
        var path = await context.Repairs.Where(x => x.Id == repairId).Select(x => new { x.PdfPath }).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Repair), repairId);

        return await storage.CreateSignedUrlAsync(
            path.PdfPath ?? throw new NotFoundException("PDF", repairId), DocumentLinks.Lifetime, cancellationToken);
    }
}
