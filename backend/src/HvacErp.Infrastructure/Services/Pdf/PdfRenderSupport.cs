using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using HvacErp.Application.Common;
using HvacErp.Application.Pdf;
using HvacErp.Domain.Entities;

namespace HvacErp.Infrastructure.Services.Pdf;

/// <summary>
/// Shared building blocks for the template-driven QuestPDF generators: page/font resolution from
/// <see cref="PdfPageConfig"/>, the common header (brand logo/title + document-meta + client panel)
/// and footer, and table cell styling. Fixed captions and amounts come from the template language's
/// <see cref="PdfLabels"/>. Each generator keeps only its document-specific section switch.
/// </summary>
internal static partial class PdfRenderSupport
{
    static PdfRenderSupport() => Configure();

    /// <summary>Idempotently registers the QuestPDF Community licence. Call from each generator's static ctor.</summary>
    public static void Configure() => QuestPDF.Settings.License = LicenseType.Community;

    public static PageSize ResolvePageSize(string? size) =>
        string.Equals(size, "Letter", StringComparison.OrdinalIgnoreCase) ? PageSizes.Letter : PageSizes.A4;

    public static string ResolveFontFamily(string? family) =>
        string.IsNullOrWhiteSpace(family) ? Fonts.Calibri : family;

    public static string SafeColor(string? hex, string fallback) =>
        hex is not null && HexColor().IsMatch(hex) ? hex : fallback;

    public static IContainer HeaderCell(IContainer container) =>
        container.DefaultTextStyle(x => x.Bold()).PaddingVertical(4).BorderBottom(1).BorderColor(Colors.Grey.Darken1);

    public static IContainer BodyCell(IContainer container) =>
        container.PaddingVertical(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);

    /// <summary>Resolved section heading, or null when the admin cleared the title (→ no heading rendered).</summary>
    public static string? SectionTitle(PdfSection section) =>
        string.IsNullOrWhiteSpace(section.TitleOverride) ? null : section.TitleOverride!.Trim();

    public static void Heading(this ColumnDescriptor column, string? title, double baseFontSize)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            column.Item().Text(title).Bold().FontSize((float)baseFontSize + 1);
        }
    }

    /// <summary>
    /// The document header shared by all three PDFs: an optional brand logo image, an optional brand
    /// title, an optional right-hand meta block (the caller supplies its lines) and an optional
    /// client panel (the caller supplies label/value pairs, already filtered by
    /// <see cref="PdfHeaderConfig.ClientFields"/>).
    /// </summary>
    /// <summary>One right-aligned line in the header's document-meta block.</summary>
    public sealed record MetaLine(string Text, bool Bold = false, float Size = 10, string? Color = null);

    public static void ComposeHeader(
        IContainer container,
        PdfHeaderConfig header,
        PdfRenderContext context,
        IReadOnlyList<MetaLine> metaLines,
        string clientPanelLabel,
        Client client,
        PdfLabels l)
    {
        var hasLogo = header.ShowLogo && context.LogoImage is { Length: > 0 };

        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(brand =>
                {
                    if (hasLogo)
                    {
                        brand.Item().Height(38).AlignLeft().Image(context.LogoImage!).FitHeight();
                    }
                    if (header.ShowTitle)
                    {
                        var title = string.IsNullOrWhiteSpace(header.TitleOverride)
                            ? context.PortalTitle
                            : header.TitleOverride!.Trim();
                        brand.Item().PaddingTop(hasLogo ? 4 : 0).Text(title).FontSize(20).Bold();
                    }
                });

                if (header.ShowDocumentMeta && metaLines.Count > 0)
                {
                    row.ConstantItem(220).Column(col =>
                    {
                        foreach (var line in metaLines)
                        {
                            var span = col.Item().AlignRight().Text(line.Text).FontSize(line.Size);
                            if (line.Bold)
                            {
                                span.Bold();
                            }
                            if (line.Color is not null)
                            {
                                span.FontColor(line.Color);
                            }
                        }
                    });
                }
            });

            if (header.ShowClientBlock)
            {
                column.Item().PaddingTop(10).Background(Colors.Grey.Lighten3).Padding(8).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(clientPanelLabel).FontSize(8).FontColor(Colors.Grey.Darken1);
                        col.Item().Text(client.FullName).Bold();
                        if (header.ClientFields.Contains("address") && !string.IsNullOrWhiteSpace(client.Address))
                        {
                            col.Item().Text(string.IsNullOrWhiteSpace(client.City)
                                ? client.Address!
                                : $"{client.Address}, {client.City}");
                        }
                    });
                    row.RelativeItem().Column(col =>
                    {
                        if (header.ClientFields.Contains("phone") && !string.IsNullOrWhiteSpace(client.Phone))
                        {
                            col.Item().Text($"{l.Phone}: {client.Phone}");
                        }
                        if (header.ClientFields.Contains("mobile") && !string.IsNullOrWhiteSpace(client.Mobile))
                        {
                            col.Item().Text($"{l.Mobile}: {client.Mobile}");
                        }
                        if (header.ClientFields.Contains("email") && !string.IsNullOrWhiteSpace(client.Email))
                        {
                            col.Item().Text($"{l.Email}: {client.Email}");
                        }
                    });
                });
            }
        });
    }

    public static void ComposeFooter(IContainer container, PdfFooterConfig footer, PdfLabels l)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten1).PaddingTop(5).Column(column =>
        {
            foreach (var line in footer.Lines)
            {
                column.Item().AlignCenter().Text(line).FontSize(8);
            }

            if (footer.ShowPageNumbers)
            {
                column.Item().AlignCenter().Text(text =>
                {
                    text.Span($"{l.Page} ").FontSize(8);
                    text.CurrentPageNumber().FontSize(8);
                    text.Span($" {l.PageOf} ").FontSize(8);
                    text.TotalPages().FontSize(8);
                });
            }
        });
    }

    /// <summary>Renders a free-text (<c>custom.text</c>) section: optional bold heading + body paragraph.</summary>
    public static void ComposeCustomText(this ColumnDescriptor column, PdfSection section, double baseFontSize)
    {
        if (string.IsNullOrWhiteSpace(section.TitleOverride) && string.IsNullOrWhiteSpace(section.Body))
        {
            return;
        }

        column.Item().Column(col =>
        {
            col.Heading(SectionTitle(section), baseFontSize);
            if (!string.IsNullOrWhiteSpace(section.Body))
            {
                col.Item().Text(section.Body!.Trim()).FontSize((float)baseFontSize - 1);
            }
        });
    }

    /// <summary>
    /// Renders the free-hand floor-plan sketch section: optional heading + one bordered SVG per
    /// non-empty sheet (captioned "Sheet N" in the template language when there is more than one), each scaled to fit a fixed
    /// box with its aspect ratio kept. No-op when the sketch is empty or unparseable.
    /// </summary>
    public static void ComposeFloorPlanSketch(
        this ColumnDescriptor column, PdfSection section, double baseFontSize, string? sketchJson, PdfLabels l)
    {
        var sheets = FloorPlanSketchSvg.RenderSheets(sketchJson);
        if (sheets.Count == 0)
        {
            return;
        }

        const float maxWidth = 380f;
        const float maxHeight = 300f;
        // The box's padding comes out of the space the SVG is fitted into, so the aspect ratio has to
        // hold for the inner box, not the outer one. 1pt of slack absorbs float rounding in QuestPDF's
        // FitWidth check; without both, every document with a sketch threw a DocumentLayoutException.
        const float padding = 3f;
        const float slack = 1f;

        column.Item().Column(col =>
        {
            col.Heading(SectionTitle(section), baseFontSize);

            var sheetNumber = 0;
            foreach (var svg in sheets)
            {
                sheetNumber++;
                var width = maxWidth;
                var height = (float)((width - 2 * padding) / svg.AspectRatio) + 2 * padding + slack;
                if (height > maxHeight)
                {
                    height = maxHeight;
                    width = (float)((height - 2 * padding - slack) * svg.AspectRatio) + 2 * padding;
                }

                col.Item().PaddingTop(sheetNumber > 1 ? 6 : 0).Column(sheet =>
                {
                    if (sheets.Count > 1)
                    {
                        sheet.Item().Text($"{l.Sheet} {sheetNumber}").FontSize((float)baseFontSize - 1).FontColor(Colors.Grey.Darken1);
                    }
                    sheet.Item().AlignLeft()
                        .Width(width).Height(height)
                        .Border(0.5f).BorderColor(Colors.Grey.Lighten1)
                        .Padding(padding)
                        .Svg(svg.Content);
                });
            }
        });
    }

    public static bool Option(this PdfSection section, string key, bool fallback = true) =>
        section.Options.TryGetValue(key, out var value) ? value : fallback;

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}
