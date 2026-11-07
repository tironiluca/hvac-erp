using HvacErp.Application.Pdf;
using HvacErp.Domain.Enums;

namespace HvacErp.Application.Dtos;

/// <summary>One row in the template picker for a document type.</summary>
public record PdfTemplateListItemDto(
    int Id,
    PdfDocumentType DocumentType,
    string Name,
    bool IsActive,
    bool IsSystemDefault,
    DateTime UpdatedAt,
    string? UpdatedBy);

/// <summary>A template with its full layout (<c>GET /api/pdf-templates/{id}</c>).</summary>
public record PdfTemplateDto(
    int Id,
    PdfDocumentType DocumentType,
    string Name,
    bool IsActive,
    bool IsSystemDefault,
    DateTime UpdatedAt,
    string? UpdatedBy,
    PdfTemplateDefinition Definition);

/// <summary>Create (<c>POST</c>) or replace (<c>PUT</c>) a template. <c>DocumentType</c> is honoured on create only.</summary>
public record UpsertPdfTemplateRequest(
    PdfDocumentType DocumentType,
    string Name,
    PdfTemplateDefinition Definition);

/// <summary>Rename + duplicate an existing template into a new inactive one.</summary>
public record DuplicatePdfTemplateRequest(string Name);

/// <summary>Render a preview PDF from an unsaved definition against <see cref="PdfSampleData"/>.</summary>
public record PdfTemplatePreviewRequest(
    PdfDocumentType DocumentType,
    PdfTemplateDefinition Definition);

// --- Catalog (drives the editor UI) -----------------------------------------------------------

/// <summary>Everything the editor may use for one document type.</summary>
public record PdfTemplateCatalogDto(
    PdfDocumentType DocumentType,
    IReadOnlyList<PdfSectionCatalogDto> Sections,
    IReadOnlyList<PdfFieldOptionDto> ClientFields,
    IReadOnlyList<string> FontFamilies);

/// <summary>A section the editor may place, with its columns and options.</summary>
public record PdfSectionCatalogDto(
    string Type,
    string LabelKey,
    bool Repeatable,
    bool SupportsBody,
    IReadOnlyList<PdfColumnOptionDto> Columns,
    IReadOnlyList<PdfOptionToggleDto> Options);

/// <summary>A selectable table column in the template editor.</summary>
public record PdfColumnOptionDto(string Key, string LabelKey, bool DefaultOn);

/// <summary>A boolean option a section supports.</summary>
public record PdfOptionToggleDto(string Key, string LabelKey, bool DefaultOn);

/// <summary>A selectable client field in the template header.</summary>
public record PdfFieldOptionDto(string Key, string LabelKey);
