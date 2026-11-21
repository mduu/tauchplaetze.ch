using System.Text.Json;

namespace Tauchplaetze.Core.Features.Echolot;

/// <summary>Eine neue Funktion aus `news.json`: Tag und Titel mit Text pro Sprache (AGENTS.md, «News on the Echolot Page»).</summary>
internal record NewsItem(DateOnly Date, IReadOnlyDictionary<string, NewsText> Texts)
{
    /// <summary>Der Text in der Sprache, sonst auf Deutsch.</summary>
    public NewsText In(string culture) => Texts.TryGetValue(culture, out var text) ? text : Texts["de"];
}

internal record NewsText(string Title, string Text);

/// <summary>Liest die Neuigkeiten aus der eingebetteten Datei `Features/Echolot/news.json`.</summary>
internal static class EcholotNews
{
    private const string ResourceName = "Tauchplaetze.Echolot.news.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Lazy<IReadOnlyList<NewsItem>> Items = new(Read);

    public static IReadOnlyList<NewsItem> All => Items.Value;

    private static IReadOnlyList<NewsItem> Read()
    {
        using var stream = typeof(EcholotNews).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Ressource {ResourceName} fehlt.");
        return JsonSerializer.Deserialize<List<NewsItem>>(stream, JsonOptions) ?? [];
    }
}
