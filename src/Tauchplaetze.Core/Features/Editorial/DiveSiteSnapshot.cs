using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tauchplaetze.Core.Features.Editorial;

/// <summary>
/// Speicherformat einer Version: der Inhalt als JSON mit `snapshotVersion` (ADR-008).
/// </summary>
/// <remarks>
/// Format 1 vom 17.10.2026 hatte zuerst keine Einstiege, bis 18.10.2026 keine Hinweise, keine Skizze und keine Fotos. Fehlen sie, gilt eine leere Liste.
/// Neue Felder nur optional anhängen.
/// </remarks>
internal static class DiveSiteSnapshot
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static byte[] ToJson(DiveSiteContent content)
    {
        var normalized = content with
        {
            Translations = [.. content.Translations.OrderBy(t => t.Culture, StringComparer.Ordinal)],
            EntryPoints = content.EntryPointList,
            Media = content.MediaList,
            OtherNames = OtherNames.Normalize(content.OtherNameList),
            Notices = [.. content.NoticeList.Select(n => n with { Texts = [.. n.Texts.OrderBy(t => t.Culture, StringComparer.Ordinal)] })],
            Sketch = [.. content.SketchList.Select(f => f with
            {
                Texts = [.. f.Texts.Where(t => !string.IsNullOrWhiteSpace(t.Label)).Select(t => t with { Label = t.Label!.Trim() }).OrderBy(t => t.Culture, StringComparer.Ordinal)],
            })],
        };
        var node = JsonSerializer.SerializeToNode(normalized, JsonOptions)!.AsObject();
        node.Remove("entryPointList");
        node.Remove("noticeList");
        node.Remove("sketchList");
        node.Remove("mediaList");
        node.Remove("otherNameList");
        RemoveEmpty(node, "otherNames");
        RemoveEmpty(node, "media");
        if (node["heroMediaId"] is null)
        {
            node.Remove("heroMediaId");
        }

        RemoveEmpty(node, "notices");
        RemoveEmpty(node, "sketch");
        node["snapshotVersion"] = CurrentVersion;
        return JsonSerializer.SerializeToUtf8Bytes(node, JsonOptions);
    }

    public static DiveSiteContent FromJson(byte[] json)
    {
        var content = JsonSerializer.Deserialize<DiveSiteContent>(json, JsonOptions)!;
        return content with { EntryPoints = content.EntryPoints ?? [], Notices = content.Notices ?? [], Sketch = content.Sketch ?? [], Media = content.Media ?? [], OtherNames = content.OtherNames ?? [] };
    }

    /// <remarks>Ohne Hinweise und Skizze bleibt das JSON wie vor dem 18.10.2026. So erkennt der Hash auch bei alten Versionen «keine Änderung».</remarks>
    private static void RemoveEmpty(JsonObject node, string property)
    {
        if (node[property] is JsonArray { Count: 0 })
        {
            node.Remove(property);
        }
    }

    public static string HashOf(byte[] json) => Convert.ToHexStringLower(SHA256.HashData(json));
}
