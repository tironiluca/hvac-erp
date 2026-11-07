using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using HvacErp.Application.Common;
using HvacErp.Application.Pdf;
using HvacErp.Domain.Entities;
using ZXing;
using ZXing.Common;
using static HvacErp.Infrastructure.Services.Pdf.PdfRenderSupport;

namespace HvacErp.Infrastructure.Services.Pdf;

/// <summary>
/// Field work-order PDF for a scheduled installation. Renders against the admin-authored
/// <see cref="PdfTemplateDefinition"/>; still carries the Code128 barcode (rendered as SVG via
/// ZXing.Net — QuestPDF has no barcode support) and the sign-off block.
/// </summary>
public class QuestPdfInstallationDocumentGenerator : IInstallationDocumentGenerator
{
    static QuestPdfInstallationDocumentGenerator() => Configure();

    public byte[] Generate(ScheduledInstallation installation, string slotLabel, PdfRenderContext context)
    {
        var def = context.Definition;
        var page = def.Page;
        var l = PdfLabels.For(page.Language);

        var document = Document.Create(container =>
        {
            container.Page(p =>
            {
                p.Size(ResolvePageSize(page.Size));
                p.Margin(page.Margin);
                p.DefaultTextStyle(x => x.FontSize((float)page.BaseFontSize).FontFamily(ResolveFontFamily(page.FontFamily)));

                p.Header().Element(c => ComposeHeader(
                    c, def.Header, context, MetaLines(installation, slotLabel, l), l.ClientHeading, installation.Quote.Client, l));
                p.Content().Element(c => ComposeContent(c, installation, def, l));
                p.Footer().Element(c => ComposeFooter(c, def.Footer, l));
            });
        });

        return document.GeneratePdf();
    }

    private static List<MetaLine> MetaLines(ScheduledInstallation installation, string slotLabel, PdfLabels l) =>
    [
        new($"{l.InstallationSheetNumber} {installation.Id}", Bold: true),
        new($"{installation.Date:dd/MM/yyyy} — {slotLabel}"),
        new($"{l.QuoteReference} {installation.Quote.Number}/{installation.Quote.Version}.{installation.Quote.Revision}", Size: 9),
    ];

    private static void ComposeContent(IContainer container, ScheduledInstallation installation, PdfTemplateDefinition def, PdfLabels l)
    {
        var baseFont = def.Page.BaseFontSize;

        container.PaddingTop(15).Column(column =>
        {
            column.Spacing(12);

            foreach (var section in def.Sections.Where(s => s.Enabled))
            {
                switch (section.Type)
                {
                    case "installation.site":
                        ComposeSite(column, section, installation);
                        break;
                    case "installation.crew":
                        ComposeCrew(column, section, baseFont, installation, l);
                        break;
                    case "installation.notes":
                        ComposeNotes(column, section, baseFont, installation);
                        break;
                    case "installation.floorPlan":
                        column.ComposeFloorPlanSketch(section, baseFont, installation.FloorPlanSketchJson, l);
                        break;
                    case "installation.barcode":
                        ComposeBarcode(column, section, baseFont, installation);
                        break;
                    case "installation.signatures":
                        ComposeSignatures(column, section, l);
                        break;
                    case "custom.text":
                        column.ComposeCustomText(section, baseFont);
                        break;
                }
            }
        });
    }

    private static void ComposeSite(ColumnDescriptor column, PdfSection section, ScheduledInstallation installation)
    {
        var site = ResolveInstallationSite(installation);
        if (string.IsNullOrWhiteSpace(site))
        {
            return;
        }

        var label = SectionTitle(section);
        column.Item().Text(string.IsNullOrWhiteSpace(label) ? site : $"{label}: {site}").FontSize(10).Bold();
    }

    private static void ComposeCrew(
        ColumnDescriptor column, PdfSection section, double baseFont, ScheduledInstallation installation, PdfLabels l)
    {
        column.Item().Column(col =>
        {
            col.Heading(SectionTitle(section), baseFont);

            if (installation.Installers.Count == 0)
            {
                col.Item().Text(l.NoInstallerAssigned).FontSize((float)baseFont - 1).Italic();
                return;
            }

            foreach (var crew in installation.Installers)
            {
                col.Item().Text($"• {crew.Installer.Name}").FontSize((float)baseFont - 1);
            }
        });
    }

    private static void ComposeNotes(
        ColumnDescriptor column, PdfSection section, double baseFont, ScheduledInstallation installation)
    {
        if (string.IsNullOrWhiteSpace(installation.Notes))
        {
            return;
        }

        column.Item().Column(col =>
        {
            col.Heading(SectionTitle(section), baseFont);
            col.Item().Text(installation.Notes).FontSize((float)baseFont - 1);
        });
    }

    private static void ComposeBarcode(
        ColumnDescriptor column, PdfSection section, double baseFont, ScheduledInstallation installation)
    {
        var barcodeContent = $"INST-{installation.Id}";

        var writer = new BarcodeWriterSvg
        {
            Format = BarcodeFormat.CODE_128,
            Options = new EncodingOptions { Height = 60, Width = 260, Margin = 0 },
        };
        var svgImage = writer.Write(barcodeContent);

        column.Item().Column(col =>
        {
            col.Heading(SectionTitle(section), baseFont);
            col.Item().Height(70).Width(280).Svg(svgImage.Content);
            if (section.Option("showText"))
            {
                col.Item().Text(barcodeContent).FontSize((float)baseFont - 1).FontColor(Colors.Grey.Darken1);
            }
        });
    }

    private static void ComposeSignatures(ColumnDescriptor column, PdfSection section, PdfLabels l)
    {
        var client = section.Option("clientSignature");
        var technician = section.Option("technicianSignature");
        if (!client && !technician)
        {
            return;
        }

        column.Item().PaddingTop(10).Row(row =>
        {
            if (client)
            {
                row.RelativeItem().PaddingRight(10).Column(col =>
                {
                    col.Item().Height(60).BorderBottom(1).BorderColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(4).Text(l.ClientSignature).FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            }
            if (technician)
            {
                row.RelativeItem().PaddingLeft(client ? 10 : 0).Column(col =>
                {
                    col.Item().Height(60).BorderBottom(1).BorderColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(4).Text(l.TechnicianSignature).FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            }
        });
    }

    /// <summary>
    /// The originating quote's installation-site override if set, otherwise the client's own
    /// address (resolved the same way the quote and repair PDFs do it).
    /// </summary>
    private static string? ResolveInstallationSite(ScheduledInstallation installation)
    {
        var quote = installation.Quote;
        var useOverride = !string.IsNullOrWhiteSpace(quote.InstallationAddress);
        var address = useOverride ? quote.InstallationAddress : quote.Client.Address;
        var city = useOverride ? quote.InstallationCity : quote.Client.City;

        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(city) ? address : $"{address}, {city}";
    }
}
