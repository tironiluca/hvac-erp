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
        var fileName = $"{(repair.Type == RepairType.Revision ? "revisione" : "riparazione")}-{repair.Id}.pdf";
        var path = $"repairs/{repair.Id}/{fileName}";

        using var stream = new MemoryStream(pdfBytes);
        var url = await storage.UploadAsync(path, stream, "application/pdf", cancellationToken);

        repair.PdfUrl = url;
        await context.SaveChangesAsync(cancellationToken);

        return url;
    }
}
