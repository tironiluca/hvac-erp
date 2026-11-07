using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using HvacErp.Application.Common;
using HvacErp.Application.Pdf;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;
using static HvacErp.Infrastructure.Services.Pdf.PdfRenderSupport;

namespace HvacErp.Infrastructure.Services.Pdf;

/// <summary>
/// Field work-order PDF for a repair or revision. Renders against the admin-authored
/// <see cref="PdfTemplateDefinition"/> in <see cref="PdfRenderContext"/>; the shared page/header/
/// footer helpers keep it visually in step with the quote and installation PDFs.
/// </summary>
public class QuestPdfRepairDocumentGenerator : IRepairDocumentGenerator
{
    static QuestPdfRepairDocumentGenerator() => Configure();

    public byte[] Generate(Repair repair, PdfRenderContext context)
    {
        var def = context.Definition;
        var page = def.Page;
        var l = PdfLabels.For(page.Language);
        var typeLabel = repair.Type == RepairType.Revision ? l.Revision : l.Repair;

        var document = Document.Create(container =>
        {
            container.Page(p =>
            {
                p.Size(ResolvePageSize(page.Size));
                p.Margin(page.Margin);
                p.DefaultTextStyle(x => x.FontSize((float)page.BaseFontSize).FontFamily(ResolveFontFamily(page.FontFamily)));

                p.Header().Element(c => ComposeHeader(
                    c, def.Header, context, MetaLines(repair, typeLabel, l), l.ClientHeading, repair.Client, l));
                p.Content().Element(c => ComposeContent(c, repair, def, l));
                p.Footer().Element(c => ComposeFooter(c, def.Footer, l));
            });
        });

        return document.GeneratePdf();
    }

    private static List<MetaLine> MetaLines(Repair repair, string typeLabel, PdfLabels l)
    {
        var lines = new List<MetaLine>
        {
            new($"{l.RepairSheetNumber} {repair.Id}", Bold: true),
            new($"{typeLabel} — {l.DatedOn} {repair.InsertedAt:dd/MM/yyyy}"),
        };
        if (repair.Urgent)
        {
            lines.Add(new(l.Urgent, Bold: true, Color: Colors.Red.Darken2));
        }
        return lines;
    }

    private static void ComposeContent(IContainer container, Repair repair, PdfTemplateDefinition def, PdfLabels l)
    {
        var baseFont = def.Page.BaseFontSize;

        container.PaddingTop(15).Column(column =>
        {
            column.Spacing(10);

            foreach (var section in def.Sections.Where(s => s.Enabled))
            {
                switch (section.Type)
                {
                    case "repair.details":
                        ComposeDetails(column, section, repair, l);
                        break;
                    case "repair.interventionSite":
                        ComposeInterventionSite(column, section, repair);
                        break;
                    case "repair.description":
                        ComposeDescription(column, section, repair, baseFont);
                        break;
                    case "repair.appointments":
                        ComposeAppointments(column, section, baseFont, repair.ScheduleEntries, l);
                        break;
                    case "repair.serviceItems":
                        ComposeServiceItems(column, section, baseFont, repair.ServiceItems, l);
                        break;
                    case "repair.floorPlan":
                        column.ComposeFloorPlanSketch(section, baseFont, repair.FloorPlanSketchJson, l);
                        break;
                    case "repair.total":
                        ComposeTotal(column, repair, def.Page, l);
                        break;
                    case "custom.text":
                        column.ComposeCustomText(section, baseFont);
                        break;
                }
            }
        });
    }

    private static void ComposeDetails(ColumnDescriptor column, PdfSection section, Repair repair, PdfLabels l)
    {
        var cells = new (string Key, string Text)[]
        {
            ("brand", $"{l.Brand}: {repair.Brand ?? "-"}"),
            ("unitCount", $"{l.UnitCount}: {repair.UnitCount}"),
            ("splitCount", $"{l.SplitCount}: {repair.SplitCount}"),
            ("installationDate", $"{l.ScheduledDate}: {(repair.InstallationDate is { } date ? $"{date:dd/MM/yyyy}" : "-")}"),
            ("predisposed", $"{l.Prepared}: {(repair.Predisposed ? l.Yes : l.No)}"),
            ("allInclusive", $"{l.FlatRate}: {(repair.AllInclusive ? l.Yes : l.No)}"),
        }.Where(c => section.Columns.Contains(c.Key)).ToArray();
        if (cells.Length == 0)
        {
            return;
        }

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(d =>
            {
                d.RelativeColumn();
                d.RelativeColumn();
                d.RelativeColumn();
                d.RelativeColumn();
            });
            foreach (var cell in cells)
            {
                table.Cell().PaddingVertical(3).Text(cell.Text);
            }
        });
    }

    private static void ComposeInterventionSite(ColumnDescriptor column, PdfSection section, Repair repair)
    {
        var site = ResolveInterventionSite(repair);
        if (string.IsNullOrWhiteSpace(site))
        {
            return;
        }

        var label = SectionTitle(section);
        column.Item().Text(string.IsNullOrWhiteSpace(label) ? site : $"{label}: {site}")
            .FontSize(9).FontColor(Colors.Grey.Darken2);
    }

    private static void ComposeDescription(ColumnDescriptor column, PdfSection section, Repair repair, double baseFont)
    {
        if (string.IsNullOrWhiteSpace(repair.Description))
        {
            return;
        }

        column.Item().Column(col =>
        {
            col.Heading(SectionTitle(section), baseFont);
            col.Item().Text(repair.Description).FontSize((float)baseFont - 1);
        });
    }

    private static void ComposeAppointments(
        ColumnDescriptor column, PdfSection section, double baseFont, ICollection<RepairScheduleEntry> entries, PdfLabels l)
    {
        if (entries.Count == 0 || section.Columns.Count == 0)
        {
            return;
        }

        var cols = new (string Key, float Width, string Header)[]
        {
            ("date", 1f, l.Date),
            ("installer", 1f, l.Installer),
            ("notes", 2f, l.Notes),
        }.Where(c => section.Columns.Contains(c.Key)).ToArray();
        if (cols.Length == 0)
        {
            return;
        }

        column.Item().Column(inner =>
        {
            inner.Heading(SectionTitle(section), baseFont);
            inner.Item().Table(table =>
            {
                table.ColumnsDefinition(d => { foreach (var c in cols) d.RelativeColumn(c.Width); });
                table.Header(h => { foreach (var c in cols) h.Cell().Element(HeaderCell).Text(c.Header); });

                foreach (var entry in entries.OrderBy(e => e.Date))
                {
                    foreach (var c in cols)
                    {
                        table.Cell().Element(BodyCell).Text(c.Key switch
                        {
                            "date" => $"{entry.Date:dd/MM/yyyy}",
                            "installer" => entry.Installer?.Name ?? "-",
                            "notes" => entry.Notes ?? "-",
                            _ => string.Empty,
                        });
                    }
                }
            });
        });
    }

    private static void ComposeServiceItems(
        ColumnDescriptor column, PdfSection section, double baseFont, ICollection<RepairServiceItem> items, PdfLabels l)
    {
        if (items.Count == 0 || section.Columns.Count == 0)
        {
            return;
        }

        column.Item().Column(inner =>
        {
            inner.Heading(SectionTitle(section), baseFont);
            inner.Item().Table(table => QuestPdfQuoteDocumentGenerator.LineItemTable(
                table, section.Columns,
                items.Select(i => (i.ServiceItem.Name, i.Quantity, i.UnitPrice)), l));
        });
    }

    private static void ComposeTotal(ColumnDescriptor column, Repair repair, PdfPageConfig page, PdfLabels l)
    {
        var total = repair.ServiceItems.Sum(s => s.Quantity * s.UnitPrice) + repair.Rounding;
        var accent = SafeColor(page.AccentColor, "#BBDEFB");

        column.Item().PaddingTop(10).Background(accent).Padding(12).Row(row =>
        {
            row.RelativeItem().Text(repair.AllInclusive ? l.FlatRateTotal : l.RepairTotal).FontSize(11);
            row.ConstantItem(150).AlignRight().Text(l.Money(total)).FontSize(18).Bold();
        });
    }

    /// <summary>
    /// Where the work happens: the originating quote's installation-site override if it has one,
    /// otherwise the client's address (resolved the same way the quote PDF does it).
    /// </summary>
    private static string? ResolveInterventionSite(Repair repair)
    {
        var useOverride = repair.Quote is not null && !string.IsNullOrWhiteSpace(repair.Quote.InstallationAddress);
        var address = useOverride ? repair.Quote!.InstallationAddress : repair.Client.Address;
        var city = useOverride ? repair.Quote!.InstallationCity : repair.Client.City;

        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(city) ? address : $"{address}, {city}";
    }
}
