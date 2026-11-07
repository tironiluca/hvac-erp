using HvacErp.Application.Dtos;
using HvacErp.Application.Pdf;
using HvacErp.Domain.Enums;

namespace HvacErp.Application.Interfaces;

/// <summary>
/// Manages the admin-editable PDF layout templates and resolves the active one for the QuestPDF
/// generators. One template per <see cref="PdfDocumentType"/> is active at a time.
/// </summary>
public interface IPdfTemplateService
{
    /// <summary>The sections / columns / options available for a document type — drives the editor UI.</summary>
    PdfTemplateCatalogDto GetCatalog(PdfDocumentType documentType);

    /// <summary>Lists templates, optionally for one document type.</summary>
    Task<List<PdfTemplateListItemDto>> ListAsync(PdfDocumentType? documentType, CancellationToken cancellationToken = default);

    /// <summary>Gets one template with its definition.</summary>
    Task<PdfTemplateDto> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The layout the generator should render — the active template's definition, or the built-in
    /// <see cref="PdfTemplateDefaults"/> when no row is active (fresh / un-seeded database).
    /// </summary>
    Task<PdfTemplateDefinition> GetActiveDefinitionAsync(PdfDocumentType documentType, CancellationToken cancellationToken = default);

    /// <summary>Validates and stores a new template.</summary>
    Task<PdfTemplateDto> CreateAsync(UpsertPdfTemplateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Validates and replaces a template's name and definition.</summary>
    Task<PdfTemplateDto> UpdateAsync(int id, UpsertPdfTemplateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Copies a template under a new name (inactive).</summary>
    Task<PdfTemplateDto> DuplicateAsync(int id, DuplicatePdfTemplateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Makes this template the active one for its document type (deactivates the previous).</summary>
    Task<PdfTemplateDto> SetActiveAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Rewrites a template's definition from <see cref="PdfTemplateDefaults"/> (the "Restore defaults" action).</summary>
    Task<PdfTemplateDto> RestoreDefaultsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Deletes a non-active, non-system template.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Renders a preview PDF from an unsaved definition against <see cref="PdfSampleData"/>.</summary>
    byte[] RenderPreview(PdfTemplatePreviewRequest request);
}
