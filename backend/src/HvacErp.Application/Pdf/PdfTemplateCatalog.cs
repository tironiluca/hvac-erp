using HvacErp.Domain.Enums;

namespace HvacErp.Application.Pdf;

/// <summary>
/// The set of sections the editor may place in a template for a given <see cref="PdfDocumentType"/>,
/// and — for table sections — the columns available, and for others the option toggles available.
/// Drives the Angular editor (it renders checkboxes straight from this) and validation
/// (<see cref="PdfTemplateValidator"/>). Section keys map 1:1 to a compose helper in the matching
/// QuestPDF generator. Labels are i18n keys resolved on the frontend, not text.
/// </summary>
public static class PdfTemplateCatalog
{
    /// <summary>Client-panel field keys the header may list (shared across all document types).</summary>
    public static readonly IReadOnlyList<PdfFieldOption> ClientFields =
    [
        new("fullName", "pdfTemplates.clientField.fullName"),
        new("address", "pdfTemplates.clientField.address"),
        new("phone", "pdfTemplates.clientField.phone"),
        new("mobile", "pdfTemplates.clientField.mobile"),
        new("email", "pdfTemplates.clientField.email"),
    ];

    /// <summary>Font families offered in the page-setup panel (all QuestPDF built-ins).</summary>
    public static readonly IReadOnlyList<string> FontFamilies =
        ["Calibri", "Arial", "Times New Roman", "Lato", "Courier New", "Verdana"];

    /// <summary>The section catalog for one document type.</summary>
    public static IReadOnlyList<PdfSectionCatalogEntry> For(PdfDocumentType documentType) => documentType switch
    {
        PdfDocumentType.Quote => Quote,
        PdfDocumentType.Repair => Repair,
        PdfDocumentType.Installation => Installation,
        _ => [],
    };

    /// <summary>True for <c>custom.text</c> — the one section that may appear more than once in a template.</summary>
    public static bool IsRepeatable(string sectionType) => sectionType == CustomText.Type;

    public static readonly PdfSectionCatalogEntry CustomText = new(
        "custom.text", "pdfTemplates.section.customText", Repeatable: true, SupportsBody: true);

    // Expression-bodied properties (not field initializers) so there is no static-init ordering
    // dependency on the shared column lists declared further down.
    private static IReadOnlyList<PdfSectionCatalogEntry> Quote =>
    [
        new("quote.documentTitle", "pdfTemplates.section.quote.documentTitle", SupportsBody: true),
        new("quote.installationSite", "pdfTemplates.section.quote.installationSite"),
        new("quote.externalUnits", "pdfTemplates.section.quote.externalUnits", Columns: UnitColumns),
        new("quote.internalUnits", "pdfTemplates.section.quote.internalUnits", Columns: UnitColumns),
        new("quote.ductedSystem", "pdfTemplates.section.quote.ductedSystem", Columns: DuctedColumns),
        new("quote.extras", "pdfTemplates.section.quote.extras"),
        new("quote.serviceItems", "pdfTemplates.section.quote.serviceItems", Columns: LineItemColumns),
        new("quote.pricingSummary", "pdfTemplates.section.quote.pricingSummary", Options:
        [
            new("showEnea", "pdfTemplates.option.showEnea", DefaultOn: true),
            new("showRaee", "pdfTemplates.option.showRaee", DefaultOn: true),
            new("showSfBlock", "pdfTemplates.option.showSfBlock", DefaultOn: true),
            new("showWarrantyNote", "pdfTemplates.option.showWarrantyNote", DefaultOn: true),
        ]),
        new("quote.positionNotes", "pdfTemplates.section.quote.positionNotes"),
        new("quote.floorPlan", "pdfTemplates.section.quote.floorPlan"),
        CustomText,
    ];

    private static IReadOnlyList<PdfSectionCatalogEntry> Repair =>
    [
        new("repair.details", "pdfTemplates.section.repair.details", Columns:
        [
            new("brand", "pdfTemplates.column.brand", DefaultOn: true),
            new("unitCount", "pdfTemplates.column.unitCount", DefaultOn: true),
            new("splitCount", "pdfTemplates.column.splitCount", DefaultOn: true),
            new("installationDate", "pdfTemplates.column.installationDate", DefaultOn: true),
            new("predisposed", "pdfTemplates.column.predisposed", DefaultOn: true),
            new("allInclusive", "pdfTemplates.column.allInclusive", DefaultOn: true),
        ]),
        new("repair.interventionSite", "pdfTemplates.section.repair.interventionSite"),
        new("repair.description", "pdfTemplates.section.repair.description"),
        new("repair.appointments", "pdfTemplates.section.repair.appointments", Columns:
        [
            new("date", "pdfTemplates.column.date", DefaultOn: true),
            new("installer", "pdfTemplates.column.installer", DefaultOn: true),
            new("notes", "pdfTemplates.column.notes", DefaultOn: true),
        ]),
        new("repair.serviceItems", "pdfTemplates.section.repair.serviceItems", Columns: LineItemColumns),
        new("repair.floorPlan", "pdfTemplates.section.repair.floorPlan"),
        new("repair.total", "pdfTemplates.section.repair.total"),
        CustomText,
    ];

    private static IReadOnlyList<PdfSectionCatalogEntry> Installation =>
    [
        new("installation.site", "pdfTemplates.section.installation.site"),
        new("installation.crew", "pdfTemplates.section.installation.crew"),
        new("installation.notes", "pdfTemplates.section.installation.notes"),
        new("installation.floorPlan", "pdfTemplates.section.installation.floorPlan"),
        new("installation.barcode", "pdfTemplates.section.installation.barcode", Options:
        [
            new("showText", "pdfTemplates.option.showBarcodeText", DefaultOn: true),
        ]),
        new("installation.signatures", "pdfTemplates.section.installation.signatures", Options:
        [
            new("clientSignature", "pdfTemplates.option.clientSignature", DefaultOn: true),
            new("technicianSignature", "pdfTemplates.option.technicianSignature", DefaultOn: true),
        ]),
        CustomText,
    ];

    private static IReadOnlyList<PdfColumnOption> UnitColumns =>
    [
        new("model", "pdfTemplates.column.model", DefaultOn: true),
        new("quantity", "pdfTemplates.column.quantity", DefaultOn: true),
        new("energyClass", "pdfTemplates.column.energyClass", DefaultOn: true),
        new("gas", "pdfTemplates.column.gas", DefaultOn: true),
        new("price", "pdfTemplates.column.price", DefaultOn: true),
    ];

    private static IReadOnlyList<PdfColumnOption> DuctedColumns =>
    [
        new("type", "pdfTemplates.column.type", DefaultOn: true),
        new("ventCount", "pdfTemplates.column.ventCount", DefaultOn: true),
        new("plenum", "pdfTemplates.column.plenum", DefaultOn: true),
        new("remote", "pdfTemplates.column.remote", DefaultOn: true),
    ];

    private static IReadOnlyList<PdfColumnOption> LineItemColumns =>
    [
        new("description", "pdfTemplates.column.description", DefaultOn: true),
        new("quantity", "pdfTemplates.column.quantity", DefaultOn: true),
        new("unitPrice", "pdfTemplates.column.unitPrice", DefaultOn: true),
        new("total", "pdfTemplates.column.total", DefaultOn: true),
    ];
}

/// <summary>A section type the catalog allows, with the table columns and boolean options it supports.</summary>
/// <param name="Type">Stable section key stored in <c>PdfSection.Type</c>.</param>
/// <param name="LabelKey">i18n key for the editor.</param>
/// <param name="Repeatable">May appear more than once in a template (only <c>custom.text</c>).</param>
/// <param name="SupportsBody">Uses <c>PdfSection.Body</c> free text (title block subtitle, custom text).</param>
/// <param name="Columns">Selectable table columns, in default order (empty for non-table sections).</param>
/// <param name="Options">Boolean toggles the section supports.</param>
public sealed record PdfSectionCatalogEntry(
    string Type,
    string LabelKey,
    bool Repeatable = false,
    bool SupportsBody = false,
    IReadOnlyList<PdfColumnOption>? Columns = null,
    IReadOnlyList<PdfOptionToggle>? Options = null)
{
    public IReadOnlyList<PdfColumnOption> Columns { get; init; } = Columns ?? [];
    public IReadOnlyList<PdfOptionToggle> Options { get; init; } = Options ?? [];
}

/// <summary>Catalog entry for a table column.</summary>
public sealed record PdfColumnOption(string Key, string LabelKey, bool DefaultOn = false);

/// <summary>Catalog entry for a section's boolean option.</summary>
public sealed record PdfOptionToggle(string Key, string LabelKey, bool DefaultOn = false);

/// <summary>Catalog entry for a client header field.</summary>
public sealed record PdfFieldOption(string Key, string LabelKey);
