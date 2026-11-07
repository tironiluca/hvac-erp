using HvacErp.Application.Common;
using HvacErp.Application.Pdf;
using HvacErp.Domain.Enums;

namespace HvacErp.Application.Tests.Validation;

public class PdfTemplateValidatorTests
{
    [Theory]
    [InlineData(PdfDocumentType.Quote)]
    [InlineData(PdfDocumentType.Repair)]
    [InlineData(PdfDocumentType.Installation)]
    public void Shipped_defaults_pass_validation(PdfDocumentType type)
    {
        PdfTemplateValidator.Validate(type, PdfTemplateDefaults.Default(type));
    }

    [Theory]
    [InlineData(PdfDocumentType.Quote)]
    [InlineData(PdfDocumentType.Repair)]
    [InlineData(PdfDocumentType.Installation)]
    public void Defaults_round_trip_through_the_stored_json_shape(PdfDocumentType type)
    {
        var json = PdfJson.Serialize(PdfTemplateDefaults.Default(type));

        PdfTemplateValidator.Validate(type, PdfJson.Deserialize(json));
        Assert.Equal(json, PdfJson.Serialize(PdfJson.Deserialize(json)));
    }

    public static TheoryData<Action<PdfTemplateDefinition>> InvalidMutations => new()
    {
        d => d.Page.Size = "A3",
        d => d.Page.Margin = 4,
        d => d.Page.BaseFontSize = 20,
        d => d.Page.FontFamily = "Comic Sans",
        d => d.Page.AccentColor = "blue",
        d => d.Header.ClientFields.Add("shoeSize"),
        d => d.Header.ClientFields.Add(d.Header.ClientFields[0]),
        d => d.Sections.Add(new PdfSection { Type = "quote.nope" }),
        d => d.Sections.Add(new PdfSection { Type = d.Sections[0].Type }),
        d => d.Sections[0].Options["notAnOption"] = true,
        d => d.Footer.Lines.AddRange(Enumerable.Repeat("x", 7)),
        d => d.Footer.Lines.Add(new string('x', 301)),
    };

    [Theory]
    [MemberData(nameof(InvalidMutations))]
    public void Invalid_quote_templates_are_rejected(Action<PdfTemplateDefinition> mutate)
    {
        var definition = PdfTemplateDefaults.Default(PdfDocumentType.Quote);
        mutate(definition);

        Assert.Throws<BusinessRuleException>(() => PdfTemplateValidator.Validate(PdfDocumentType.Quote, definition));
    }

    [Fact]
    public void Custom_text_sections_may_repeat()
    {
        var definition = PdfTemplateDefaults.Default(PdfDocumentType.Quote);
        definition.Sections.Add(new PdfSection { Type = PdfTemplateCatalog.CustomText.Type, Body = "One" });
        definition.Sections.Add(new PdfSection { Type = PdfTemplateCatalog.CustomText.Type, Body = "Two" });

        PdfTemplateValidator.Validate(PdfDocumentType.Quote, definition);
    }
}
