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

    public static IReadOnlyList<SketchFeature> Read(string? json) =>
        json is null ? [] : JsonSerializer.Deserialize<List<SketchFeature>>(json, Options) ?? [];

    public static string? Write(IReadOnlyList<SketchFeature> sketch) =>
        sketch.Count == 0
            ? null
            : JsonSerializer.Serialize(sketch.Select(f => f with { Texts = [.. f.Texts.Where(t => !string.IsNullOrWhiteSpace(t.Label)).Select(t => t with { Label = t.Label!.Trim() })] }), Options);
}
