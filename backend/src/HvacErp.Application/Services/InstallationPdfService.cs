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
        var fileName = $"installation-{installation.Id}.pdf";
        var path = $"installations/{installation.Id}/{fileName}";

        using var stream = new MemoryStream(pdfBytes);
        // A PDF generated under an older file name is replaced, not left beside the new one.
        var previous = installation.ReportPdfPath;
        installation.ReportPdfPath = await storage.UploadAsync(path, stream, "application/pdf", cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        if (previous is not null && previous != installation.ReportPdfPath)
        {
            await storage.TryDeleteAsync(previous, cancellationToken);
        }

        return await storage.CreateSignedUrlAsync(installation.ReportPdfPath, DocumentLinks.Lifetime, cancellationToken);
    }

    public async Task<string> GetUrlAsync(int installationId, CancellationToken cancellationToken = default)
    {
        // Through the filtered set: only someone who can read the record gets a link to its PDF.
        var path = await context.ScheduledInstallations.Where(x => x.Id == installationId).Select(x => new { x.ReportPdfPath }).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(ScheduledInstallation), installationId);

        return await storage.CreateSignedUrlAsync(
            path.ReportPdfPath ?? throw new NotFoundException("PDF", installationId), DocumentLinks.Lifetime, cancellationToken);
    }
}
