using HvacErp.Domain.Enums;

namespace HvacErp.Domain.Entities;

/// <summary>
/// An admin-editable layout definition for one of the generated PDFs (<see cref="PdfDocumentType"/>).
/// The QuestPDF generators render against the <b>active</b> template for their document type instead
/// of a hardcoded layout. <see cref="DefinitionJson"/> is a serialized
/// <c>HvacErp.Application.Pdf.PdfTemplateDefinition</c> (stored as <c>jsonb</c>); adding a
/// field to that shape is not a schema migration.
/// </summary>
public class PdfTemplate
{
    public int Id { get; set; }

    /// <summary>Which PDF this template lays out. A row's <see cref="DocumentType"/> never changes after creation.</summary>
    public PdfDocumentType DocumentType { get; set; }

    /// <summary>Admin-facing name, unique within a <see cref="DocumentType"/>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Exactly one row per <see cref="DocumentType"/> has this set — it is the layout the generator
    /// uses. Enforced by a partial unique index; flipped atomically by <c>SetActiveAsync</c>.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// The seeded built-in template (one per type). Cannot be deleted; "Restore defaults" rewrites
    /// its <see cref="DefinitionJson"/> from <c>PdfTemplateDefaults</c>.
    /// </summary>
    public bool IsSystemDefault { get; set; }

    /// <summary>Serialized <c>PdfTemplateDefinition</c> (page setup, header, ordered sections, footer). <c>jsonb</c>.</summary>
    public string DefinitionJson { get; set; } = "{}";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Supabase email / user id of the admin who last saved (via the Phase 8d <c>ICurrentUser</c>).</summary>
    public string? UpdatedBy { get; set; }
}
