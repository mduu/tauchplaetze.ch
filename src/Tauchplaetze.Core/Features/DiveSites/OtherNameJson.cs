using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Tauchplaetze.Core.Features.DiveSites;

/// <summary>Speicherformat der weiteren Namen in <c>DiveSite.OtherNames</c>: JSON-Array, Umlaute im Klartext (sonst wird «ä» zu sechs Zeichen).</summary>
internal static class OtherNameJson
{
    private static readonly JsonSerializerOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string? Write(IReadOnlyList<string> names) => names.Count > 0 ? JsonSerializer.Serialize(names, Options) : null;

    /// <summary>Ungültiges JSON ergibt keine Namen statt einer Ausnahme, damit Platzseite und Verzeichnis weiter laufen.</summary>
    public static IReadOnlyList<string> Read(string? json, ILogger? logger = null)
    {
        if (json is null)
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, Options) ?? [];
        }
        catch (JsonException exception)
        {
            logger?.LogWarning(exception, "Invalid other names JSON ignored");
            return [];
        }
    }
}
