using System.Globalization;
using System.Text.Json;

namespace Tauchplaetze.Core.Features.Susv.Sources;

/// <summary>Ein Eintrag einer Karte auf susv.ch, so weit wir ihn brauchen.</summary>
internal record SusvMapEntry(int Id, string Title, string? Teaser, DateTimeOffset? PublishedAt, double Latitude, double Longitude, string DetailUrl);

/// <summary>Die Einträge eines Themas in einer Sprache. <see cref="Skipped"/>: Einträge, die sich nicht lesen liessen.</summary>
internal record SusvMap(IReadOnlyList<SusvMapEntry> Entries, int Skipped)
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    private static readonly string[] DateFormats = ["yyyy-MM-dd HH:mm:ss.ffffff", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd"];

    /// <remarks>
    /// Die Seite ist im Aufbau (Antwort SUSV, 25.11.2026). Der Parser ist darum nachsichtig:
    /// Die Liste darf unter `content.entries`, `entries` oder als Array an der Wurzel stehen.
    /// Zahlen dürfen als Text kommen, das Datum als Objekt oder als Text, der Link relativ.
    /// Unbekannte Felder zählen nicht. Ein einzelner unlesbarer Eintrag fällt weg, statt den Call scheitern zu lassen.
    /// Fehlt die Liste ganz, ist das ein Fehler: Dann gilt der Call als gescheitert, und nichts wird entfernt.
    /// </remarks>
    public static SusvMap Parse(string json, Uri baseUri)
    {
        using var document = JsonDocument.Parse(json);
        var list = FindEntries(document.RootElement) ?? throw new FormatException("Keine Liste der Einträge gefunden.");
        var entries = new List<SusvMapEntry>();
        var skipped = 0;
        foreach (var element in list.EnumerateArray())
        {
            if (Read(element, baseUri) is { } entry)
            {
                entries.Add(entry);
            }
            else
            {
                skipped++;
            }
        }

        return new SusvMap(entries.DistinctBy(e => e.Id).ToList(), skipped);
    }

    private static JsonElement? FindEntries(JsonElement root) => root.ValueKind switch
    {
        JsonValueKind.Array => root,
        JsonValueKind.Object when Property(root, "content") is { ValueKind: JsonValueKind.Object } content && Property(content, "entries") is { ValueKind: JsonValueKind.Array } entries => entries,
        JsonValueKind.Object when Property(root, "entries") is { ValueKind: JsonValueKind.Array } entries => entries,
        _ => null,
    };

    private static SusvMapEntry? Read(JsonElement element, Uri baseUri)
    {
        if (element.ValueKind != JsonValueKind.Object
            || Integer(Property(element, "id")) is not { } id
            || Text(Property(element, "title")) is not { } title
            || Number(Property(element, "lat")) is not { } latitude
            || Number(Property(element, "lng") ?? Property(element, "lon")) is not { } longitude
            || !InSwitzerland(latitude, longitude))
        {
            return null;
        }

        var url = Link(Text(Property(element, "detailUrl")), baseUri);
        if (url is null)
        {
            return null;
        }

        return new SusvMapEntry(
            id,
            SusvRules.Shorten(SusvRules.OneLine(title), SusvRules.TitleLength),
            Text(Property(element, "teaserText")) is { } teaser ? SusvRules.Shorten(SusvRules.OneLine(teaser), SusvRules.TeaserLength) : null,
            Date(Property(element, "date")),
            Math.Round(latitude, 6),
            Math.Round(longitude, 6),
            url);
    }

    /// <summary>Grosszügig um die Schweiz: Ein Punkt ausserhalb ist ein Fehler der Quelle, etwa vertauschte Koordinaten.</summary>
    private static bool InSwitzerland(double latitude, double longitude) => latitude is > 45.6 and < 48.0 && longitude is > 5.8 and < 10.7;

    /// <summary>Nur Links auf die Seite des SUSV. Ein relativer Link gilt ab der Basis.</summary>
    private static string? Link(string? value, Uri baseUri)
    {
        if (value is null || !Uri.TryCreate(baseUri, value, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !uri.Host.EndsWith(baseUri.Host, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var link = uri.ToString();
        return link.Length <= SusvRules.UrlLength ? link : null;
    }

    private static DateTimeOffset? Date(JsonElement? element)
    {
        var text = element switch
        {
            { ValueKind: JsonValueKind.Object } value => Text(Property(value, "date")),
            { ValueKind: JsonValueKind.String } value => value.GetString(),
            _ => null,
        };
        if (text is null)
        {
            return null;
        }

        if (DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return new DateTimeOffset(local, Zone.GetUtcOffset(local));
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : null;
    }

    private static JsonElement? Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : null;

    private static string? Text(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.String } value && value.GetString() is { } text && !string.IsNullOrWhiteSpace(text) ? text : null;

    private static int? Integer(JsonElement? element) => element switch
    {
        { ValueKind: JsonValueKind.Number } value when value.TryGetInt32(out var number) && number > 0 => number,
        { ValueKind: JsonValueKind.String } value when int.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0 => number,
        _ => null,
    };

    private static double? Number(JsonElement? element) => element switch
    {
        { ValueKind: JsonValueKind.Number } value => value.GetDouble(),
        { ValueKind: JsonValueKind.String } value when double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => number,
        _ => null,
    };
}
