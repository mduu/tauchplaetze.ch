using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tauchplaetze.Core.Features.Editorial;

/// <summary>
/// Speicherformat einer Version: der Inhalt als JSON mit `snapshotVersion` (ADR-008).
/// </summary>
/// <remarks>
/// Format 1 vom 17.10.2026 hatte zuerst keine Einstiege, bis 18.10.2026 keine Hinweise. Fehlen sie, gilt eine leere Liste.
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
            Notices = [.. content.NoticeList.Select(n => n with { Texts = [.. n.Texts.OrderBy(t => t.Culture, StringComparer.Ordinal)] })],
        };
        var node = JsonSerializer.SerializeToNode(normalized, JsonOptions)!.AsObject();
        node.Remove("entryPointList");
        node.Remove("noticeList");
        RemoveEmptyNotices(node);
        node["snapshotVersion"] = CurrentVersion;
        return JsonSerializer.SerializeToUtf8Bytes(node, JsonOptions);
    }

    public static DiveSiteContent FromJson(byte[] json)
    {
        var content = JsonSerializer.Deserialize<DiveSiteContent>(json, JsonOptions)!;
        return content with { EntryPoints = content.EntryPoints ?? [], Notices = content.Notices ?? [] };
    }

    /// <remarks>Ohne Hinweise bleibt das JSON wie vor dem 18.10.2026. So erkennt der Hash auch bei alten Versionen «keine Änderung».</remarks>
    private static void RemoveEmptyNotices(JsonObject node)
    {
        if (node["notices"] is JsonArray { Count: 0 })
        {
            node.Remove("notices");
        }
    }

    public static string HashOf(byte[] json) => Convert.ToHexStringLower(SHA256.HashData(json));
}
