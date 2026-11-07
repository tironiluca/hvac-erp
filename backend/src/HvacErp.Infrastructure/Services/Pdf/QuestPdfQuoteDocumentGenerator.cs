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
/// Customer-facing quote PDF. Renders against the admin-authored <see cref="PdfTemplateDefinition"/>
/// in <see cref="PdfRenderContext"/> (page setup, header, ordered sections, footer) rather than a
/// hardcoded layout; <see cref="PdfTemplateDefaults"/> reproduces the original layout exactly.
/// </summary>
public class QuestPdfQuoteDocumentGenerator : IQuoteDocumentGenerator
{
    static QuestPdfQuoteDocumentGenerator() => Configure();

    public byte[] Generate(Quote quote, string vatLabel, PdfRenderContext context)
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
                    c, def.Header, context, MetaLines(quote, def.Header, l), l.QuoteRecipient, quote.Client, l));
                p.Content().Element(c => ComposeContent(c, quote, vatLabel, def, l));
                p.Footer().Element(c => ComposeFooter(c, def.Footer, l));
            });
        });

        return document.GeneratePdf();
    }

    private static List<MetaLine> MetaLines(Quote quote, PdfHeaderConfig header, PdfLabels l)
    {
        var lines = new List<MetaLine>
        {
            new($"{l.QuoteNumber} {quote.Number}/{quote.Version}.{quote.Revision}", Bold: true),
            new($"{l.DatedOn} {quote.CreatedAt:dd/MM/yyyy}"),
        };
        if (header.ShowAgent && quote.Operator is not null)
        {
            lines.Add(new($"{l.Agent}: {quote.Operator.Name}", Size: 9));
        }
        return lines;
    }

    private static void ComposeContent(IContainer container, Quote quote, string vatLabel, PdfTemplateDefinition def, PdfLabels l)
    {
        var baseFont = def.Page.BaseFontSize;

        container.PaddingTop(15).Column(column =>
        {
            column.Spacing(10);

            foreach (var section in def.Sections.Where(s => s.Enabled))
            {
                switch (section.Type)
                {
                    case "quote.documentTitle":
                        ComposeDocumentTitle(column, section, baseFont);
                        break;
                    case "quote.installationSite":
                        ComposeInstallationSite(column, section, quote);
                        break;
                    case "quote.externalUnits":
                        ComposeUnitsTable(column, section, baseFont,
                            quote.Products.Where(pr => pr.Role == ProductRole.ExternalUnit).ToList(), l);
                        break;
                    case "quote.internalUnits":
                        ComposeUnitsTable(column, section, baseFont,
                            quote.Products.Where(pr => pr.Role == ProductRole.InternalUnit).ToList(), l);
                        break;
                    case "quote.ductedSystem":
                        ComposeDuctedTable(column, section, baseFont, quote.DuctedUnits, l);
                        break;
                    case "quote.extras":
                        ComposeExtras(column, section, baseFont, quote, l);
                        break;
                    case "quote.serviceItems":
                        ComposeServiceItemsTable(column, section, baseFont, quote.ServiceItems, l);
                        break;
                    case "quote.pricingSummary":
                        ComposePricingSummary(column, section, quote, vatLabel, def.Page, l);
                        break;
                    case "quote.positionNotes":
                        ComposePositionNotes(column, quote, baseFont);
                        break;
                    case "quote.floorPlan":
                        column.ComposeFloorPlanSketch(section, baseFont, quote.FloorPlanSketchJson, l);
                        break;
                    case "custom.text":
                        column.ComposeCustomText(section, baseFont);
                        break;
                }
            }
        });
    }

    private static void ComposeDocumentTitle(ColumnDescriptor column, PdfSection section, double baseFont)
    {
        var title = SectionTitle(section);
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(section.Body))
        {
            return;
        }

        column.Item().Column(inner =>
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                inner.Item().Text(title).FontSize((float)baseFont + 3).Bold();
            }
            if (!string.IsNullOrWhiteSpace(section.Body))
            {
                inner.Item().Text(section.Body!.Trim()).FontSize((float)baseFont - 1).FontColor(Colors.Grey.Darken2);
            }
        });
    }

    private static void ComposeInstallationSite(ColumnDescriptor column, PdfSection section, Quote quote)
    {
        var site = ResolveInstallationSite(quote);
        if (string.IsNullOrWhiteSpace(site))
        {
            return;
        }

        var label = SectionTitle(section);
        var text = string.IsNullOrWhiteSpace(label) ? site : $"{label}: {site}";
        column.Item().Text(text).FontSize(9).FontColor(Colors.Grey.Darken2);
    }

    private static void ComposeUnitsTable(
        ColumnDescriptor column, PdfSection section, double baseFont, List<QuoteProduct> units, PdfLabels l)
    {
        if (units.Count == 0 || section.Columns.Count == 0)
        {
            return;
        }

        var cols = new (string Key, float Width, string Header, bool Right)[]
        {
            ("model", 3f, l.Model, false),
            ("quantity", 1f, l.Quantity, false),
            ("energyClass", 2f, l.EnergyClass, false),
            ("gas", 1f, l.Gas, false),
            ("price", 1f, l.Price, true),
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
                table.Header(h =>
                {
                    foreach (var c in cols)
                    {
                        var cell = h.Cell().Element(HeaderCell);
                        (c.Right ? cell.AlignRight() : cell).Text(c.Header);
                    }
                });

                foreach (var unit in units)
                {
                    foreach (var c in cols)
                    {
                        var cell = table.Cell().Element(BodyCell);
                        if (c.Right) cell = cell.AlignRight();
                        cell.Text(c.Key switch
                        {
                            "model" => $"{unit.Product.Brand} {unit.Product.Model}",
                            "quantity" => unit.Quantity.ToString(),
                            "energyClass" => $"{unit.Product.CoolingClass}/{unit.Product.HeatingClass}",
                            "gas" => unit.Product.RefrigerantGas ?? "-",
                            "price" => l.Money(unit.DiscountedPrice),
                            _ => string.Empty,
                        });
                    }
                }
            });
        });
    }

    private static void ComposeDuctedTable(
        ColumnDescriptor column, PdfSection section, double baseFont, ICollection<DuctedUnit> ductedUnits, PdfLabels l)
    {
        if (ductedUnits.Count == 0 || section.Columns.Count == 0)
        {
            return;
        }

        var cols = new (string Key, float Width, string Header)[]
        {
            ("type", 2f, l.DuctType),
            ("ventCount", 1f, l.VentCount),
            ("plenum", 1f, l.Plenum),
            ("remote", 1f, l.Remote),
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

                foreach (var ducted in ductedUnits)
                {
                    foreach (var outlet in ducted.Outlets.OrderBy(o => o.Position))
                    {
                        foreach (var c in cols)
                        {
                            table.Cell().Element(BodyCell).Text(c.Key switch
                            {
                                "type" => outlet.SystemType,
                                "ventCount" => outlet.VentCount.ToString(),
                                "plenum" => ducted.PlenumQuantity.ToString(),
                                "remote" => ducted.RemoteQuantity.ToString(),
                                _ => string.Empty,
                            });
                        }
                    }
                }
            });
        });
    }

    private static void ComposeExtras(ColumnDescriptor column, PdfSection section, double baseFont, Quote quote, PdfLabels l)
    {
        var extras = new List<string>();
        if (quote.Predisposed)
        {
            extras.Add(l.PreparedSystem);
        }
        foreach (var extra in quote.Extras.OrderBy(e => e.ExtraType.SortOrder))
        {
            extras.Add(extra.ExtraType.QuantityBased
                ? $"{extra.ExtraType.Label}: {extra.Quantity:0.##}"
                : extra.ExtraType.Label);
        }
        if (extras.Count == 0)
        {
            return;
        }

        column.Item().Column(col =>
        {
            col.Heading(SectionTitle(section), baseFont);
            foreach (var extra in extras)
            {
                col.Item().Text($"• {extra}").FontSize((float)baseFont - 1);
            }
        });
    }

    private static void ComposeServiceItemsTable(
        ColumnDescriptor column, PdfSection section, double baseFont, ICollection<QuoteServiceItem> items, PdfLabels l)
    {
        if (items.Count == 0 || section.Columns.Count == 0)
        {
            return;
        }

        column.Item().Column(inner =>
        {
            inner.Heading(SectionTitle(section), baseFont);
            inner.Item().Table(table => LineItemTable(
                table, section.Columns,
                items.Select(i => (i.ServiceItem.Name, i.Quantity, i.UnitPrice)), l));
        });
    }

    private static void ComposePricingSummary(
        ColumnDescriptor column, PdfSection section, Quote quote, string vatLabel, PdfPageConfig page, PdfLabels l)
    {
        var accent = SafeColor(page.AccentColor, "#BBDEFB");

        column.Item().PaddingTop(10).Column(col =>
        {
            col.Item().Background(accent).Padding(12).Row(row =>
            {
                row.RelativeItem().Column(inner =>
                {
                    inner.Item().Text($"{l.SupplyAndInstallationTotal} — {vatLabel}").FontSize(11);
                    if (section.Option("showEnea") && quote.EneaEligible)
                    {
                        inner.Item().Text(l.EneaEligible).FontSize(9).FontColor(Colors.Green.Darken2);
                    }
                    if (section.Option("showRaee") && quote.RaeeEligible)
                    {
                        inner.Item().Text(l.RaeeIncluded).FontSize(9).FontColor(Colors.Green.Darken2);
                    }
                });
                row.ConstantItem(150).AlignRight().Text(l.Money(quote.FinalPrice)).FontSize(18).Bold();
            });

            if (section.Option("showSfBlock") && quote is { SfRequested: true, SfPrice: { } sfPrice })
            {
                ComposeInvoiceDiscountBlock(col, section, quote, sfPrice, l);
            }
        });
    }

    /// <summary>
    /// Invoice-discount ("sconto in fattura") block: the grossed-up invoice total struck through, next to the 50%
    /// amount the customer actually pays (legacy InsPrev.vb GeneraPdfScontoFattura).
    /// </summary>
    private static void ComposeInvoiceDiscountBlock(
        ColumnDescriptor column, PdfSection section, Quote quote, decimal sfPrice, PdfLabels l)
    {
        column.Item().PaddingTop(8).Background(Colors.Grey.Lighten3).Padding(12).Column(col =>
        {
            col.Item().Text(l.InvoiceDiscount).Bold().FontSize(10);

            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text(l.InvoiceTotalWithVat).FontSize(10);
                row.ConstantItem(130).AlignRight().Text(l.Money(sfPrice)).FontSize(11).Strikethrough();
            });

            if (section.Option("showWarrantyNote") && quote.Products.Any(p => p.Warranty))
            {
                col.Item().Text(l.WarrantyIncluded).FontSize(9).FontColor(Colors.Grey.Darken2);
            }
            if (section.Option("showEnea") && quote.EneaEligible)
            {
                col.Item().Text(l.EneaPaperworkIncluded).FontSize(9).FontColor(Colors.Grey.Darken2);
            }

            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text(l.TotalWithInvoiceDiscount).Bold().FontSize(10);
                row.ConstantItem(130).AlignRight().Text(l.Money(sfPrice / 2m)).FontSize(13).Bold();
            });
        });
    }

    private static void ComposePositionNotes(ColumnDescriptor column, Quote quote, double baseFont)
    {
        if (!string.IsNullOrWhiteSpace(quote.InstallationPositionNotes))
        {
            column.Item().PaddingTop(5).Text(quote.InstallationPositionNotes).FontSize((float)baseFont - 1).Italic();
        }
    }

    /// <summary>Shared description / quantity / unit price / total table body used by quote + repair line items.</summary>
    internal static void LineItemTable(
        TableDescriptor table, List<string> columns, IEnumerable<(string Name, decimal Quantity, decimal UnitPrice)> rows, PdfLabels l)
    {
        var cols = new (string Key, float Width, string Header, bool Right)[]
        {
            ("description", 4f, l.Description, false),
            ("quantity", 1f, l.Quantity, true),
            ("unitPrice", 1f, l.UnitPrice, true),
            ("total", 1f, l.Total, true),
        }.Where(c => columns.Contains(c.Key)).ToArray();
        if (cols.Length == 0)
        {
            cols = [("description", 1f, l.Description, false)];
        }

        table.ColumnsDefinition(d => { foreach (var c in cols) d.RelativeColumn(c.Width); });
        table.Header(h =>
        {
            foreach (var c in cols)
            {
                var cell = h.Cell().Element(HeaderCell);
                (c.Right ? cell.AlignRight() : cell).Text(c.Header);
            }
        });

        foreach (var row in rows)
        {
            foreach (var c in cols)
            {
                var cell = table.Cell().Element(BodyCell);
                if (c.Right) cell = cell.AlignRight();
                cell.Text(c.Key switch
                {
                    "description" => row.Name,
                    "quantity" => row.Quantity.ToString("0.##"),
                    "unitPrice" => l.Money(row.UnitPrice),
                    "total" => l.Money(row.Quantity * row.UnitPrice),
                    _ => string.Empty,
                });
            }
        }
    }

    /// <summary>
    /// Per-quote installation site if overridden, otherwise the client's own address
    /// (the override + client address are treated as a unit — no field-by-field mixing).
    /// </summary>
    private static string? ResolveInstallationSite(Quote quote)
    {
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
