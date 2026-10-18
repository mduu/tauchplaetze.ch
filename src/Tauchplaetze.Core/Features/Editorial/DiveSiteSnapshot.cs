using System.Security.Cryptography;
using System.Text.Json;

namespace Tauchplaetze.Core.Features.Editorial;

/// <summary>
/// Speicherformat einer Version: der Inhalt als JSON mit `snapshotVersion` (ADR-008).
/// </summary>
/// <remarks>Format 1 vom 17.10.2026 hatte noch keine Einstiege. Fehlen sie, gilt eine leere Liste. Neue Felder nur optional anhängen.</remarks>
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
        };
        var node = JsonSerializer.SerializeToNode(normalized, JsonOptions)!.AsObject();
        node.Remove("entryPointList");
        node["snapshotVersion"] = CurrentVersion;
        return JsonSerializer.SerializeToUtf8Bytes(node, JsonOptions);
    }

    public static DiveSiteContent FromJson(byte[] json)
    {
        var content = JsonSerializer.Deserialize<DiveSiteContent>(json, JsonOptions)!;
        return content with { EntryPoints = content.EntryPoints ?? [] };
    }

    public static string HashOf(byte[] json) => Convert.ToHexStringLower(SHA256.HashData(json));
}
