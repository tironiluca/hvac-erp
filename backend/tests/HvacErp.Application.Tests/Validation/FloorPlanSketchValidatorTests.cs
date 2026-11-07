using System.Text.Json;
using HvacErp.Application.Common;
using HvacErp.Application.Pdf;

namespace HvacErp.Application.Tests.Validation;

public class FloorPlanSketchValidatorTests
{
    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    private const string Stroke = """{ "color": "#1A2B3C", "width": 3, "opacity": 1, "points": [[10, 20], [30, 40]] }""";

    private static string Sketch(string sheets, int version = 1) =>
        $$"""{ "version": {{version}}, "width": 1000, "height": 667, "sheets": [{{sheets}}] }""";

    [Fact]
    public void Null_input_stores_nothing()
    {
        Assert.Null(FloorPlanSketchValidator.Validate(null));
        Assert.Null(FloorPlanSketchValidator.Validate(Json("null")));
    }

    [Fact]
    public void Sketch_whose_sheets_are_all_empty_stores_nothing()
    {
        Assert.Null(FloorPlanSketchValidator.Validate(Json(Sketch("""{ "strokes": [] }, { "strokes": [] }"""))));
    }

    [Fact]
    public void Valid_multi_sheet_sketch_is_returned_verbatim()
    {
        var raw = Sketch($$"""{ "strokes": [{{Stroke}}] }, { "strokes": [] }""");

        Assert.Equal(Json(raw).GetRawText(), FloorPlanSketchValidator.Validate(Json(raw)));
    }

    [Fact]
    public void Legacy_single_sheet_strokes_shape_is_accepted()
    {
        var raw = $$"""{ "version": 1, "width": 1000, "height": 667, "strokes": [{{Stroke}}] }""";

        Assert.NotNull(FloorPlanSketchValidator.Validate(Json(raw)));
    }

    [Theory]
    [InlineData("""{ "color": "red", "width": 3, "opacity": 1, "points": [[1, 2]] }""")]
    [InlineData("""{ "color": "#123456", "width": 0, "opacity": 1, "points": [[1, 2]] }""")]
    [InlineData("""{ "color": "#123456", "width": 3, "opacity": 1.5, "points": [[1, 2]] }""")]
    [InlineData("""{ "color": "#123456", "width": 3, "opacity": 1, "points": [] }""")]
    [InlineData("""{ "color": "#123456", "width": 3, "opacity": 1, "points": [[1, 2, 3]] }""")]
    [InlineData("""[1, 2]""")]
    public void Malformed_strokes_are_rejected(string stroke)
    {
        Assert.Throws<BusinessRuleException>(() =>
            FloorPlanSketchValidator.Validate(Json(Sketch($$"""{ "strokes": [{{stroke}}] }"""))));
    }

    [Fact]
    public void Unsupported_version_is_rejected()
    {
        Assert.Throws<BusinessRuleException>(() =>
            FloorPlanSketchValidator.Validate(Json(Sketch($$"""{ "strokes": [{{Stroke}}] }""", version: 2))));
    }

    [Fact]
    public void More_than_eight_sheets_is_rejected()
    {
        var sheets = string.Join(",", Enumerable.Repeat($$"""{ "strokes": [{{Stroke}}] }""", 9));

        Assert.Throws<BusinessRuleException>(() => FloorPlanSketchValidator.Validate(Json(Sketch(sheets))));
    }

    [Fact]
    public void Non_object_or_sheetless_payloads_are_rejected()
    {
        Assert.Throws<BusinessRuleException>(() => FloorPlanSketchValidator.Validate(Json("[]")));
        Assert.Throws<BusinessRuleException>(() => FloorPlanSketchValidator.Validate(Json("""{ "version": 1 }""")));
    }

    [Fact]
    public void Svg_renderer_emits_one_svg_per_non_empty_sheet()
    {
        var raw = Sketch($$"""{ "strokes": [{{Stroke}}] }, { "strokes": [] }, { "strokes": [{{Stroke}}] }""");

        var sheets = FloorPlanSketchSvg.RenderSheets(raw);

        Assert.Equal(2, sheets.Count);
        Assert.Empty(FloorPlanSketchSvg.RenderSheets(null));
    }
}
