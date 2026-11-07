using System.Text.Json;
using System.Text.RegularExpressions;

namespace HvacErp.Application.Common;

/// <summary>
/// Validates the free-hand floor-plan sketch payload sent by the quote / repair / installation
/// forms and returns the raw JSON to persist in the entity's <c>jsonb</c> column (or null when there
/// is nothing to store). A sketch is one or more <c>sheets</c>, each a list of <c>strokes</c>; a
/// legacy top-level <c>strokes</c> array is accepted as a single sheet. The Angular
/// <c>arc-floor-plan-sketch</c> component owns the schema; this is the server-side backstop against
/// oversized or malformed input. Violations throw <see cref="BusinessRuleException"/> (→ HTTP 400).
/// </summary>
public static partial class FloorPlanSketchValidator
{
    private const int MaxSheets = 8;
    private const int MaxStrokes = 2_000;
    private const int MaxPoints = 100_000;
    private const int MaxRawChars = 1024 * 1024;
    private const double MaxCanvasSize = 20_000;
    private const double MaxStrokeWidth = 200;

    /// <summary>Validates a sketch payload against the size / shape limits above.</summary>
    /// <returns>The canonical raw JSON to store, or null when the sketch is empty / absent.</returns>
    public static string? Validate(JsonElement? input)
    {
        if (input is not { } element || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new BusinessRuleException("The floor-plan sketch must be a JSON object.");
        }

        var sheets = ResolveSheets(element);
        if (sheets.Count == 0)
        {
            return null;
        }

        if (sheets.Count > MaxSheets)
        {
            throw new BusinessRuleException("The floor-plan sketch has too many sheets.");
        }

        // All sheets empty — treat as "cleared", store nothing.
        if (sheets.All(sheet => StrokesOf(sheet).GetArrayLength() == 0))
        {
            return null;
        }

        var raw = element.GetRawText();
        if (raw.Length > MaxRawChars)
        {
            throw new BusinessRuleException("The floor-plan sketch is too large.");
        }

        if (!element.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || version.GetInt32() != 1)
        {
            throw new BusinessRuleException("Unsupported floor-plan sketch version.");
        }

        RequireCanvasDimension(element, "width");
        RequireCanvasDimension(element, "height");

        var totalStrokes = 0;
        var totalPoints = 0;
        foreach (var sheet in sheets)
        {
            foreach (var stroke in StrokesOf(sheet).EnumerateArray())
            {
                if (stroke.ValueKind != JsonValueKind.Object)
                {
                    throw new BusinessRuleException("Each floor-plan stroke must be a JSON object.");
                }

                var color = stroke.TryGetProperty("color", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
                if (color is null || !HexColor().IsMatch(color))
                {
                    throw new BusinessRuleException("A floor-plan stroke has an invalid colour.");
                }

                if (!TryGetNumber(stroke, "width", out var width) || width is <= 0 or > MaxStrokeWidth)
                {
                    throw new BusinessRuleException("A floor-plan stroke has an invalid width.");
                }

                if (!TryGetNumber(stroke, "opacity", out var opacity) || opacity is < 0 or > 1)
                {
                    throw new BusinessRuleException("A floor-plan stroke has an invalid opacity.");
                }

                if (!stroke.TryGetProperty("points", out var points) || points.ValueKind != JsonValueKind.Array || points.GetArrayLength() == 0)
                {
                    throw new BusinessRuleException("A floor-plan stroke has no points.");
                }

                foreach (var point in points.EnumerateArray())
                {
                    if (point.ValueKind != JsonValueKind.Array || point.GetArrayLength() != 2
                        || point[0].ValueKind != JsonValueKind.Number || point[1].ValueKind != JsonValueKind.Number)
                    {
                        throw new BusinessRuleException("A floor-plan stroke has a malformed point.");
                    }
                }

                if (++totalStrokes > MaxStrokes)
                {
                    throw new BusinessRuleException("The floor-plan sketch has too many strokes.");
                }

                totalPoints += points.GetArrayLength();
                if (totalPoints > MaxPoints)
                {
                    throw new BusinessRuleException("The floor-plan sketch has too many points.");
                }
            }
        }

        return raw;
    }

    /// <summary>The <c>sheets</c> array, or a single synthetic sheet wrapping a legacy top-level <c>strokes</c> array.</summary>
    private static List<JsonElement> ResolveSheets(JsonElement element)
    {
        if (element.TryGetProperty("sheets", out var sheets) && sheets.ValueKind == JsonValueKind.Array)
        {
            return sheets.EnumerateArray().ToList();
        }

        if (element.TryGetProperty("strokes", out var strokes) && strokes.ValueKind == JsonValueKind.Array)
        {
            return [element];
        }

        throw new BusinessRuleException("The floor-plan sketch is missing its 'sheets' array.");
    }

    private static JsonElement StrokesOf(JsonElement sheet)
    {
        if (sheet.ValueKind != JsonValueKind.Object
            || !sheet.TryGetProperty("strokes", out var strokes)
            || strokes.ValueKind != JsonValueKind.Array)
        {
            throw new BusinessRuleException("Each floor-plan sheet must have a 'strokes' array.");
        }

        return strokes;
    }

    private static void RequireCanvasDimension(JsonElement element, string name)
    {
        if (!TryGetNumber(element, name, out var size) || size is <= 0 or > MaxCanvasSize)
        {
            throw new BusinessRuleException($"The floor-plan sketch has an invalid {name}.");
        }
    }

    private static bool TryGetNumber(JsonElement parent, string name, out double value)
    {
        if (parent.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Number)
        {
            value = element.GetDouble();
            return true;
        }

        value = 0;
        return false;
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}
