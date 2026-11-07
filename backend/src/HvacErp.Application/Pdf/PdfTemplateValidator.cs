using System.Text.RegularExpressions;
using HvacErp.Application.Common;
using HvacErp.Domain.Enums;

namespace HvacErp.Application.Pdf;

/// <summary>
/// Checks a client-supplied <see cref="PdfTemplateDefinition"/> against
/// <see cref="PdfTemplateCatalog"/> before it is stored. Anything the generator could not render —
/// an unknown section type, a column/option key not in the catalog, an out-of-range page value —
/// is a <see cref="BusinessRuleException"/> (→ 400), never a silent drop.
/// </summary>
public static partial class PdfTemplateValidator
{
    /// <summary>Throws <see cref="Common.BusinessRuleException"/> when the definition uses anything the catalog does not allow.</summary>
    public static void Validate(PdfDocumentType documentType, PdfTemplateDefinition definition)
    {
        ValidatePage(definition.Page);
        ValidateHeader(definition.Header);
        ValidateSections(documentType, definition.Sections);
        ValidateFooter(definition.Footer);
    }

    private static void ValidatePage(PdfPageConfig page)
    {
        if (page.Size is not ("A4" or "Letter"))
        {
            throw new BusinessRuleException("Page size must be 'A4' or 'Letter'.");
        }
        if (page.Margin is < 8 or > 72)
        {
            throw new BusinessRuleException("Page margin must be between 8 and 72 points.");
        }
        if (page.BaseFontSize is < 7 or > 14)
        {
            throw new BusinessRuleException("Base font size must be between 7 and 14 points.");
        }
        if (!PdfTemplateCatalog.FontFamilies.Contains(page.FontFamily))
        {
            throw new BusinessRuleException($"Unknown font family '{page.FontFamily}'.");
        }
        if (!HexColor().IsMatch(page.AccentColor))
        {
            throw new BusinessRuleException("Accent colour must be a hex colour in #RRGGBB form.");
        }
        if (!PdfLabels.Supported.Contains(page.Language))
        {
            throw new BusinessRuleException(
                $"Unknown PDF language '{page.Language}'. Use one of: {string.Join(", ", PdfLabels.Supported)}.");
        }
    }

    private static void ValidateHeader(PdfHeaderConfig header)
    {
        var allowed = PdfTemplateCatalog.ClientFields.Select(f => f.Key).ToHashSet();
        foreach (var field in header.ClientFields)
        {
            if (!allowed.Contains(field))
            {
                throw new BusinessRuleException($"Unknown client field '{field}'.");
            }
        }
        if (header.ClientFields.Count != header.ClientFields.Distinct().Count())
        {
            throw new BusinessRuleException("Client fields must not repeat.");
        }
        if (header.TitleOverride is { Length: > 200 })
        {
            throw new BusinessRuleException("Header title override must be at most 200 characters.");
        }
    }

    private static void ValidateSections(PdfDocumentType documentType, List<PdfSection> sections)
    {
        var catalog = PdfTemplateCatalog.For(documentType).ToDictionary(e => e.Type);
        var seen = new HashSet<string>();

        foreach (var section in sections)
        {
            if (!catalog.TryGetValue(section.Type, out var entry))
            {
                throw new BusinessRuleException($"Unknown section '{section.Type}' for {documentType} templates.");
            }
            if (!seen.Add(section.Type) && !entry.Repeatable)
            {
                throw new BusinessRuleException($"Section '{section.Type}' may only appear once.");
            }

            var allowedColumns = entry.Columns.Select(c => c.Key).ToHashSet();
            foreach (var column in section.Columns)
            {
                if (!allowedColumns.Contains(column))
                {
                    throw new BusinessRuleException($"Unknown column '{column}' on section '{section.Type}'.");
                }
            }
            if (section.Columns.Count != section.Columns.Distinct().Count())
            {
                throw new BusinessRuleException($"Section '{section.Type}' lists a column twice.");
            }

            var allowedOptions = entry.Options.Select(o => o.Key).ToHashSet();
            foreach (var option in section.Options.Keys)
            {
                if (!allowedOptions.Contains(option))
                {
                    throw new BusinessRuleException($"Unknown option '{option}' on section '{section.Type}'.");
                }
            }

            if (section.TitleOverride is { Length: > 200 })
            {
                throw new BusinessRuleException($"Section '{section.Type}' title is too long (max 200).");
            }
            if (section.Body is { Length: > 4000 })
            {
                throw new BusinessRuleException($"Section '{section.Type}' text is too long (max 4000).");
            }
        }
    }

    private static void ValidateFooter(PdfFooterConfig footer)
    {
        if (footer.Lines.Count > 6)
        {
            throw new BusinessRuleException("A footer may have at most 6 lines.");
        }
        if (footer.Lines.Any(line => line.Length > 300))
        {
            throw new BusinessRuleException("Each footer line must be at most 300 characters.");
        }
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}
