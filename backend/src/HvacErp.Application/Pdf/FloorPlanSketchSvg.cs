using System.Globalization;
using System.Text;
using System.Text.Json;

namespace HvacErp.Application.Pdf;

/// <summary>
/// Renders a stored floor-plan sketch (the vector JSON produced by the Angular <c>arc-floor-plan-sketch</c>
/// canvas) to one standalone SVG string per non-empty sheet, for embedding in the generated PDFs via
/// QuestPDF's <c>.Svg(string)</c> element. Parses defensively — a null, empty or malformed sketch
/// yields an empty list and the caller renders nothing.
/// </summary>
public static class FloorPlanSketchSvg
{
    /// <summary>One rendered sketch sheet as standalone SVG markup plus its intrinsic size.</summary>
    /// <param name="Content">The SVG markup for one sheet.</param>
    /// <param name="Width">Intrinsic canvas width (user units) — for aspect-ratio-preserving layout.</param>
    /// <param name="Height">Intrinsic canvas height (user units).</param>
    public readonly record struct FloorPlanSvg(string Content, double Width, double Height)
    {
        public double AspectRatio => Height > 0 ? Width / Height : 1;
    }

    /// <summary>Renders each non-empty sheet of a stored sketch as a standalone SVG; empty list when there is nothing to draw.</summary>
    public static IReadOnlyList<FloorPlanSvg> RenderSheets(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var width = Number(root, "width", 900);
            var height = Number(root, "height", 640);
            if (width <= 0 || height <= 0)
            {
                return [];
            }

            var result = new List<FloorPlanSvg>();
            foreach (var sheet in ResolveSheets(root))
            {
                if (!sheet.TryGetProperty("strokes", out var strokes)
                    || strokes.ValueKind != JsonValueKind.Array
                    || strokes.GetArrayLength() == 0)
                {
                    continue;
                }

                result.Add(new FloorPlanSvg(BuildSvg(strokes, width, height), width, height));
            }

            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IEnumerable<JsonElement> ResolveSheets(JsonElement root)
    {
        if (root.TryGetProperty("sheets", out var sheets) && sheets.ValueKind == JsonValueKind.Array)
        {
            return sheets.EnumerateArray().Where(s => s.ValueKind == JsonValueKind.Object);
        }

        // Legacy single-surface shape: top-level `strokes`.
        return root.TryGetProperty("strokes", out var strokes) && strokes.ValueKind == JsonValueKind.Array
            ? [root]
            : [];
    }

    private static string BuildSvg(JsonElement strokes, double width, double height)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(width)}\" height=\"{F(height)}\" viewBox=\"0 0 {F(width)} {F(height)}\">");
        sb.Append(CultureInfo.InvariantCulture,
            $"<rect x=\"0\" y=\"0\" width=\"{F(width)}\" height=\"{F(height)}\" fill=\"#ffffff\"/>");

        foreach (var stroke in strokes.EnumerateArray())
        {
            if (stroke.ValueKind != JsonValueKind.Object
                || !stroke.TryGetProperty("points", out var points)
                || points.ValueKind != JsonValueKind.Array
                || points.GetArrayLength() == 0)
            {
                continue;
            }

            var color = stroke.TryGetProperty("color", out var c) && c.ValueKind == JsonValueKind.String
                ? c.GetString() ?? "#000000"
                : "#000000";
            var strokeWidth = Number(stroke, "width", 3);
            var opacity = Number(stroke, "opacity", 1);

            sb.Append("<polyline fill=\"none\" stroke-linecap=\"round\" stroke-linejoin=\"round\" points=\"");
            var first = true;
            foreach (var p in points.EnumerateArray())
            {
                if (p.ValueKind != JsonValueKind.Array || p.GetArrayLength() != 2
                    || p[0].ValueKind != JsonValueKind.Number || p[1].ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                if (!first)
                {
                    sb.Append(' ');
                }
                first = false;
                sb.Append(CultureInfo.InvariantCulture, $"{F(p[0].GetDouble())},{F(p[1].GetDouble())}");
            }

            sb.Append(CultureInfo.InvariantCulture,
                $"\" stroke=\"{Escape(color)}\" stroke-width=\"{F(strokeWidth)}\" opacity=\"{F(opacity)}\"/>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static double Number(JsonElement parent, string name, double fallback) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : fallback;

    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");
}
