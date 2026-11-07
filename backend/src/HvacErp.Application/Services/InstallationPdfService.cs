using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Common;
using HvacErp.Application.Interfaces;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;

namespace HvacErp.Application.Services;

/// <summary>Loads an installation, renders its work order and stores it.</summary>
public class InstallationPdfService(
    IAppDbContext context,
    IInstallationDocumentGenerator generator,
    IPdfTemplateService templates,
    IFileStorageService storage) : IInstallationPdfService
{
    public async Task<string> GenerateAndStoreAsync(int installationId, CancellationToken cancellationToken = default)
    {
        var installation = await context.ScheduledInstallations
            .Include(i => i.Quote).ThenInclude(q => q.Client)
            .Include(i => i.Installers).ThenInclude(i => i.Installer)
            .FirstOrDefaultAsync(i => i.Id == installationId, cancellationToken)
            ?? throw new NotFoundException(nameof(ScheduledInstallation), installationId);

        var slotLabel = await context.DaySlots
            .Where(s => s.Code == installation.Slot)
            .Select(s => s.Label)
            .FirstOrDefaultAsync(cancellationToken) ?? installation.Slot;

        var renderContext = await PdfRenderContextFactory.CreateAsync(
            PdfDocumentType.Installation, context, templates, storage, cancellationToken);

        var pdfBytes = generator.Generate(installation, slotLabel.ToUpperInvariant(), renderContext);
        var fileName = $"installazione-{installation.Id}.pdf";
        var path = $"installations/{installation.Id}/{fileName}";

        using var stream = new MemoryStream(pdfBytes);
        var url = await storage.UploadAsync(path, stream, "application/pdf", cancellationToken);

        installation.ReportPdfUrl = url;
        await context.SaveChangesAsync(cancellationToken);

        return url;
    }
}
