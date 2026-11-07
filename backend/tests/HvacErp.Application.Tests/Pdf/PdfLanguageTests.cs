using HvacErp.Application.Common;
using HvacErp.Application.Pdf;
using HvacErp.Application.Services;
using HvacErp.Application.Tests.Support;
using HvacErp.Domain.Entities;
using HvacErp.Domain.Enums;
using HvacErp.Infrastructure.Services.Pdf;

namespace HvacErp.Application.Tests.Pdf;

/// <summary>English is the product default; Italian stays available per template.</summary>
public class PdfLanguageTests
{
    [Fact]
    public void Labels_default_to_english_and_switch_to_italian()
    {
        Assert.Equal("Total", PdfLabels.For(null).Total);
        Assert.Equal("Total", PdfLabels.For("fr").Total);
        Assert.Equal("Totale", PdfLabels.For("it").Total);
        Assert.Equal("Totale", PdfLabels.For("IT").Total);
    }

    [Fact]
    public void Amounts_are_euros_in_both_languages()
    {
        Assert.Equal("€1,234.50", PdfLabels.For("en").Money(1234.5m));
        Assert.Equal("1.234,50 €", PdfLabels.For("it").Money(1234.5m));
    }

    [Theory]
    [InlineData(PdfDocumentType.Quote)]
    [InlineData(PdfDocumentType.Repair)]
    [InlineData(PdfDocumentType.Installation)]
    public void Default_templates_are_english_unless_italian_is_asked_for(PdfDocumentType type)
    {
        var english = PdfTemplateDefaults.Default(type);
        var italian = PdfTemplateDefaults.Default(type, PdfLabels.Italian);

        Assert.Equal(PdfLabels.English, english.Page.Language);
        Assert.Equal(PdfLabels.Italian, italian.Page.Language);
        Assert.Contains(english.Sections, s => s.TitleOverride is "FLOOR PLAN");
        Assert.Contains(italian.Sections, s => s.TitleOverride is "PLANIMETRIA");
        PdfTemplateValidator.Validate(type, english);
        PdfTemplateValidator.Validate(type, italian);
    }

    [Fact]
    public void Validator_rejects_an_unsupported_language()
    {
        var definition = PdfTemplateDefaults.Default(PdfDocumentType.Quote);
        definition.Page.Language = "fr";

        Assert.Throws<BusinessRuleException>(() => PdfTemplateValidator.Validate(PdfDocumentType.Quote, definition));
    }

    [Fact]
    public async Task Restoring_defaults_keeps_the_template_language()
    {
        await using var db = TestDb.Create();
        var row = new PdfTemplate
        {
            DocumentType = PdfDocumentType.Repair,
            Name = "Scheda",
            IsActive = true,
            DefinitionJson = PdfJson.Serialize(PdfTemplateDefaults.Default(PdfDocumentType.Repair, PdfLabels.Italian)),
        };
        db.PdfTemplates.Add(row);
        await db.SaveChangesAsync();
        var service = new PdfTemplateService(db, new FakeCurrentUser(role: UserRole.Admin), null!, null!, null!);

        var restored = await service.RestoreDefaultsAsync(row.Id);

        Assert.Equal(PdfLabels.Italian, restored.Definition.Page.Language);
        Assert.Contains(restored.Definition.Sections, s => s.TitleOverride is "DESCRIZIONE INTERVENTO");
    }

    [Theory]
    [InlineData(PdfDocumentType.Quote, PdfLabels.English)]
    [InlineData(PdfDocumentType.Quote, PdfLabels.Italian)]
    [InlineData(PdfDocumentType.Repair, PdfLabels.English)]
    [InlineData(PdfDocumentType.Repair, PdfLabels.Italian)]
    [InlineData(PdfDocumentType.Installation, PdfLabels.English)]
    [InlineData(PdfDocumentType.Installation, PdfLabels.Italian)]
    public void Every_document_renders_in_each_language(PdfDocumentType type, string language)
    {
        var service = new PdfTemplateService(null!, new FakeCurrentUser(), new QuestPdfQuoteDocumentGenerator(),
            new QuestPdfRepairDocumentGenerator(), new QuestPdfInstallationDocumentGenerator());

        var pdf = service.RenderPreview(new(type, PdfTemplateDefaults.Default(type, language)));

        Assert.Equal("%PDF"u8.ToArray(), pdf[..4]);
    }

    /// <summary>
    /// Regression: the sketch box's padding was not taken out of the aspect-ratio fit, so any
    /// document with a floor-plan sketch threw a QuestPDF layout exception. Wide and tall sketches
    /// take different sizing branches.
    /// </summary>
    [Theory]
    [InlineData(1000, 667)]
    [InlineData(667, 1000)]
    [InlineData(1000, 1000)]
    public void Floor_plan_sketches_of_any_shape_render(int width, int height)
    {
        var quote = PdfSampleData.Quote();
        quote.FloorPlanSketchJson =
            $$"""{"version":1,"width":{{width}},"height":{{height}},"sheets":[{"strokes":[{"color":"#000000","width":4,"opacity":1,"points":[[10,10],[{{width - 10}},{{height - 10}}]]}]}]}""";
        var context = new PdfRenderContext(PdfTemplateDefaults.Default(PdfDocumentType.Quote), "HVAC ERP", LogoImage: null);

        var pdf = new QuestPdfQuoteDocumentGenerator().Generate(quote, PdfSampleData.VatLabel, context);

        Assert.Equal("%PDF"u8.ToArray(), pdf[..4]);
    }
}
