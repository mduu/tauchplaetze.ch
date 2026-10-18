using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tauchplaetze.Core.Features.Editorial;

/// <summary>Das Format der Skizze in <c>DiveSite.Sketch</c>. Arten als Namen, leere Beschriftungen fallen weg.</summary>
internal static class SketchJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Ein Element mit unbekannter Art fällt weg, statt die Seite scheitern zu lassen.</summary>
    public static IReadOnlyList<SketchFeature> Read(string? json)
    {
        if (json is null)
        {
            return [];
        }

        var items = JsonSerializer.Deserialize<List<JsonElement>>(json, Options) ?? [];
        return [.. items.Select(TryRead).OfType<SketchFeature>()];
    }

    private static SketchFeature? TryRead(JsonElement item)
    {
        try
        {
            return item.Deserialize<SketchFeature>(Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? Write(IReadOnlyList<SketchFeature> sketch) =>
        sketch.Count == 0
            ? null
            : JsonSerializer.Serialize(sketch.Select(f => f with { Texts = [.. f.Texts.Where(t => !string.IsNullOrWhiteSpace(t.Label)).Select(t => t with { Label = t.Label!.Trim() })] }), Options);
}
