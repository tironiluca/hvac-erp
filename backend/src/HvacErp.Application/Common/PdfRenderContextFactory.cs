using Microsoft.EntityFrameworkCore;
using HvacErp.Application.Interfaces;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;

namespace HvacErp.Application.Common;

/// <summary>
/// Assembles the <see cref="PdfRenderContext"/> the QuestPDF generators need: the active template's
/// definition for the document type plus the <c>PortalBranding</c> title and logo image bytes
/// (best-effort — a missing/unreachable logo just yields null, never an error).
/// </summary>
public static class PdfRenderContextFactory
{
    private const string DefaultTitle = PortalBranding.DefaultPortalTitle;

    /// <summary>Builds the render context for <paramref name="documentType"/> from the active template and the portal branding.</summary>
    public static async Task<PdfRenderContext> CreateAsync(
        PdfDocumentType documentType,
        IAppDbContext context,
        IPdfTemplateService templates,
        IFileStorageService storage,
        CancellationToken cancellationToken)
    {
        var definition = await templates.GetActiveDefinitionAsync(documentType, cancellationToken);

        var branding = await context.PortalBrandings.AsNoTracking()
            .Select(b => new { b.PortalTitle, b.LogoUrl })
            .FirstOrDefaultAsync(cancellationToken);

        var title = string.IsNullOrWhiteSpace(branding?.PortalTitle) ? DefaultTitle : branding!.PortalTitle;

        byte[]? logo = null;
        if (definition.Header.ShowLogo && !string.IsNullOrWhiteSpace(branding?.LogoUrl))
        {
            logo = await storage.TryDownloadAsync(branding!.LogoUrl!, cancellationToken);
        }

        return new PdfRenderContext(definition, title, logo);
    }
}
