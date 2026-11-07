using System.Text.Json;
using System.Text.Json.Serialization;

namespace HvacErp.Application.Pdf;

/// <summary>
/// The layout of one generated PDF, stored as <c>jsonb</c> in <c>PdfTemplate.DefinitionJson</c> and
/// sent verbatim to the Angular editor. The QuestPDF generators walk <see cref="Sections"/> in order,
/// rendering each enabled one via the compose helper its <see cref="PdfSection.Type"/> maps to.
/// Every string in <see cref="PdfTemplateCatalog"/> is the authority on which section types / column
/// keys / option keys are legal — <see cref="PdfTemplateValidator"/> rejects anything else.
/// </summary>
public sealed class PdfTemplateDefinition
{
    public PdfPageConfig Page { get; set; } = new();
    public PdfHeaderConfig Header { get; set; } = new();
    public List<PdfSection> Sections { get; set; } = [];
    public PdfFooterConfig Footer { get; set; } = new();
}

/// <summary>Page size, margins, font and accent colour.</summary>
public sealed class PdfPageConfig
{
    /// <summary><c>A4</c> or <c>Letter</c>.</summary>
    public string Size { get; set; } = "A4";

    /// <summary>Page margin in points (8–72).</summary>
    public int Margin { get; set; } = 30;

    /// <summary>A QuestPDF built-in font family name (e.g. <c>Calibri</c>, <c>Arial</c>, <c>Times New Roman</c>, <c>Lato</c>).</summary>
    public string FontFamily { get; set; } = "Calibri";

    /// <summary>Base text size in points (7–14); section headings scale off this.</summary>
    public double BaseFontSize { get; set; } = 10;

    /// <summary>Background of the prominent total / summary blocks, <c>#RRGGBB</c>.</summary>
    public string AccentColor { get; set; } = "#BBDEFB";

    /// <summary>
    /// Language of the fixed captions the generators print (<c>en</c> or <c>it</c>, see
    /// <see cref="PdfLabels"/>); section headings and body text are the template's own.
    /// </summary>
    public string Language { get; set; } = PdfLabels.English;
}

/// <summary>Header layout: logo, title, document meta and client block.</summary>
public sealed class PdfHeaderConfig
{
    /// <summary>Show the uploaded brand logo image (falls back to the title text when no logo exists).</summary>
    public bool ShowLogo { get; set; } = true;

    /// <summary>Show the portal title text in the header.</summary>
    public bool ShowTitle { get; set; } = true;

    /// <summary>Overrides the <c>PortalBranding</c> title for this document only. Null → the portal title.</summary>
    public string? TitleOverride { get; set; }

    /// <summary>Show the right-hand meta block (document number, date, type line).</summary>
    public bool ShowDocumentMeta { get; set; } = true;

    /// <summary>Quote only — show the "Agente: …" line. Ignored for repair/installation.</summary>
    public bool ShowAgent { get; set; } = true;

    /// <summary>Show the client / recipient panel.</summary>
    public bool ShowClientBlock { get; set; } = true;

    /// <summary>Which client fields the panel lists, in order. Keys: <c>fullName</c>, <c>address</c>, <c>phone</c>, <c>mobile</c>, <c>email</c>.</summary>
    public List<string> ClientFields { get; set; } = ["fullName", "address", "phone", "mobile", "email"];
}

/// <summary>One body section of a template, in render order.</summary>
public sealed class PdfSection
{
    /// <summary>A <see cref="PdfTemplateCatalog"/> section key (e.g. <c>quote.externalUnits</c>, <c>custom.text</c>).</summary>
    public string Type { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    /// <summary>Overrides the section's default heading. Null/empty → the default (or no heading).</summary>
    public string? TitleOverride { get; set; }

    /// <summary>For table sections: which columns to render, in order. Must be a subset of the catalog's column keys.</summary>
    public List<string> Columns { get; set; } = [];

    /// <summary>Section-specific booleans (e.g. <c>showEnea</c>). Keys must be in the catalog entry's options.</summary>
    public Dictionary<string, bool> Options { get; set; } = [];

    /// <summary>Free-text body for <c>custom.text</c> sections. Ignored elsewhere.</summary>
    public string? Body { get; set; }
}

/// <summary>Footer lines and page numbering.</summary>
public sealed class PdfFooterConfig
{
    /// <summary>Centred footer lines (company address, contacts). Rendered in order above the page number.</summary>
    public List<string> Lines { get; set; } = [];

    public bool ShowPageNumbers { get; set; } = true;
}

/// <summary>The single <see cref="JsonSerializerOptions"/> used for both the stored <c>jsonb</c> and the HTTP wire, so they stay identical.</summary>
public static class PdfJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false,
    };

    /// <summary>Serializes a template definition for storage.</summary>
    public static string Serialize(PdfTemplateDefinition definition) => JsonSerializer.Serialize(definition, Options);

    /// <summary>Parses a stored / posted template definition.</summary>
    public static PdfTemplateDefinition Deserialize(string json) =>
        JsonSerializer.Deserialize<PdfTemplateDefinition>(json, Options) ?? new PdfTemplateDefinition();
}
