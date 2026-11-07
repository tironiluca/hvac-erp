using System.Text.Json;

namespace HvacErp.Application.Common;

/// <summary>
/// Reads the raw floor-plan sketch JSON stored in an entity's <c>jsonb</c> column back into a
/// <see cref="JsonElement"/> for the detail DTOs, so it goes over the wire as a real nested object
/// rather than an escaped string. A null / blank / unparseable value maps to null.
/// </summary>
public static class FloorPlanSketchJson
{
    /// <summary>Turns the stored <c>jsonb</c> sketch back into a <see cref="System.Text.Json.JsonElement"/> for the DTO; null when absent or unparsable.</summary>
    public static JsonElement? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
