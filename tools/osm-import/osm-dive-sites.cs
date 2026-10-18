#:property PublishAot=false

// Holt die Tauchplätze der Schweiz aus OpenStreetMap (ODbL) und ergänzt die Seeds der App.
// Übernimmt nur Name, Koordinate, Gewässer, Gemeinde und Kanton, dazu Einstiege, Ausstiege und Parkplätze als Orte des Platzes.
// Aufruf aus dem Repository: dotnet run tools/osm-import/osm-dive-sites.cs [-- --cached] [-- --exclude-source <regex>]
// Ergebnis: neue Einträge in den Seeds (src/Tauchplaetze.Core/Features/…/Seed/*.json) und tools/osm-import/out/review.md.
// Ablauf und Regeln: specs/data-sources.md, «OpenStreetMap».

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

const string Overpass = "https://overpass-api.de/api/interpreter";
const string UserAgent = "tauchplaetze.ch osm-import (https://tauchplaetze.ch)";
const string Query = """
    [out:json][timeout:300];
    area["ISO3166-1"="CH"][admin_level=2]->.ch;
    nwr["sport"="scuba_diving"](area.ch)->.sites;
    foreach.sites->.s(
      .s out tags center;
      (way(around.s:300)[natural=water][name];rel(around.s:300)[natural=water][name];way(around.s:300)[waterway~"^(river|canal)$"][name];);
      out tags center;
      (.s;node(w.s););is_in->.a;
      area.a[admin_level~"^(4|8)$"][boundary=administrative];
      out tags;
      area.a[natural=water][name];
      out tags;
    );
    """;

var root = FindRepositoryRoot();
var outDir = Path.Combine(root, "tools", "osm-import", "out");
var rawPath = Path.Combine(outDir, "overpass.json");
var waterSeedPath = Path.Combine(root, "src", "Tauchplaetze.Core", "Features", "WaterBodies", "Seed", "water-bodies.json");
var siteSeedPath = Path.Combine(root, "src", "Tauchplaetze.Core", "Features", "Editorial", "Seed", "dive-sites.json");
var excludeSource = new Regex(Option("--exclude-source") ?? "swiss-divers", RegexOptions.IgnoreCase);
Directory.CreateDirectory(outDir);

if (!args.Contains("--cached") || !File.Exists(rawPath))
{
    await File.WriteAllTextAsync(rawPath, await Download());
}

var elements = JsonNode.Parse(await File.ReadAllTextAsync(rawPath))!["elements"]!.AsArray();
var candidates = Candidates(elements).ToList();
var review = new Review();

var waterSeeds = JsonNode.Parse(await File.ReadAllTextAsync(waterSeedPath))!.AsArray();
var siteSeeds = JsonNode.Parse(await File.ReadAllTextAsync(siteSeedPath))!.AsArray();
var waterSlugs = waterSeeds.Select(w => (string)w!["slug"]!).ToHashSet();
var newWaters = new Dictionary<string, NewWater>();

var usable = new List<Candidate>();
foreach (var candidate in candidates)
{
    if (candidate.Name is null)
    {
        review.Skipped.Add($"ohne Namen: OSM {candidate.OsmId} ({Coordinates(candidate)})");
    }
    else if (excludeSource.IsMatch(candidate.Source ?? ""))
    {
        review.Skipped.Add($"Quelle ausgeschlossen ({candidate.Source}): {candidate.Name}");
    }
    else if (candidate.Water is null)
    {
        review.Skipped.Add($"kein Gewässer im Umkreis von 300 m: {candidate.Name} ({candidate.Municipality}, {Coordinates(candidate)})");
    }
    else
    {
        usable.Add(candidate);
    }
}

var sites = Group(usable, review);
var existingSlugs = siteSeeds.Select(s => Osm.Slug($"{s!["name"]} {s["municipality"]}")).ToHashSet();
var added = 0;

foreach (var site in sites.OrderBy(s => s.Water.NameDe, StringComparer.Create(CultureInfo.GetCultureInfo("de-CH"), true)).ThenBy(s => s.Name))
{
    var slug = Osm.Slug($"{site.Name} {site.Municipality}");
    if (!existingSlugs.Add(slug))
    {
        review.Duplicates.Add($"{site.Name} ({site.Municipality}): Slug «{slug}» besteht schon");
        continue;
    }

    var waterSlug = site.Water.Slug;
    if (!waterSlugs.Contains(waterSlug))
    {
        var water = newWaters.TryGetValue(waterSlug, out var known) ? known : newWaters[waterSlug] = new NewWater(site.Water);
        water.Sites.Add(site);
    }

    siteSeeds.Add(ToSeed(site, waterSlug));
    review.Added.Add(site);
    added++;
}

foreach (var water in newWaters.Values.OrderBy(w => w.Water.NameDe))
{
    waterSeeds.Add(water.ToSeed());
}

await WriteJson(waterSeedPath, waterSeeds);
await WriteJson(siteSeedPath, siteSeeds);
await File.WriteAllTextAsync(Path.Combine(outDir, "review.md"), review.Render(candidates.Count, newWaters.Values));

Console.WriteLine($"{candidates.Count} Punkte aus OSM, {added} neue Plätze, {newWaters.Count} neue Gewässer. Durchsicht: {Path.Combine(outDir, "review.md")}");
return;

string? Option(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
    {
        directory = directory.Parent;
    }

    return directory?.FullName ?? throw new InvalidOperationException("Aufruf aus dem Repository tauchplaetze.ch nötig.");
}

static async Task<string> Download()
{
    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(6) };
    http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    var delay = TimeSpan.FromSeconds(15);

    for (var attempt = 1; ; attempt++)
    {
        string problem;
        try
        {
            using var response = await http.PostAsync(Overpass, new FormUrlEncodedContent([new("data", Query)]));
            var body = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode && body.TrimStart().StartsWith('{'))
            {
                return body;
            }

            problem = $"HTTP {(int)response.StatusCode}";
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            problem = exception.GetType().Name;
        }

        if (attempt == 5)
        {
            throw new HttpRequestException($"Overpass antwortet nicht ({problem}).");
        }

        Console.WriteLine($"Overpass ausgelastet ({problem}), neuer Versuch in {delay.TotalSeconds} s.");
        await Task.Delay(delay);
        delay *= 2;
    }
}

static IEnumerable<Candidate> Candidates(JsonArray elements)
{
    Candidate? current = null;
    var waters = new List<Water>();

    foreach (var element in elements)
    {
        var tags = element!["tags"]?.AsObject();
        var type = (string)element["type"]!;

        if (type != "area" && (string?)tags?["sport"] == "scuba_diving")
        {
            if (current is not null)
            {
                yield return current with { Water = Water.Choose(waters, current) };
            }

            var (latitude, longitude) = Osm.Position(element);
            current = new Candidate($"{type}/{element["id"]}", Osm.Tag(tags, "name"), latitude, longitude, Osm.Tag(tags, "source"), null, null, null);
            waters = [];
        }
        else if (current is null)
        {
            continue;
        }
        else if (type == "area" && Osm.Tag(tags, "natural") == "water")
        {
            if (Water.From(tags!, element, current.Latitude, current.Longitude) is { } surrounding)
            {
                waters.Add(surrounding);
            }
        }
        else if (type == "area")
        {
            current = (string?)tags?["admin_level"] == "4"
                ? current with { Canton = Osm.Tag(tags, "ISO3166-2")?.Replace("CH-", "") }
                : Patterns.LakeTerritory.IsMatch(Osm.Tag(tags, "name") ?? "") ? current : current with { Municipality = Osm.Tag(tags, "name") };
        }
        else if (Water.From(tags!, element, current.Latitude, current.Longitude) is { } water)
        {
            waters.Add(water);
        }
    }

    if (current is not null)
    {
        yield return current with { Water = Water.Choose(waters, current) };
    }
}

static List<Site> Group(List<Candidate> candidates, Review review)
{
    var parkings = candidates.Where(c => Patterns.Parking.IsMatch(c.Name!)).ToList();
    var entries = candidates.Where(c => Patterns.EntryPoint.IsMatch(c.Name!)).Except(parkings).ToList();
    var sites = candidates.Except(entries).Except(parkings).Select(c => new Site(c.Name!, c.Latitude, c.Longitude, c.Water!, c.Municipality, c.Canton, [c.OsmId], [])).ToList();

    foreach (var entry in entries.OrderBy(e => e.Name))
    {
        var match = Patterns.EntryPoint.Match(entry.Name!);
        var baseName = entry.Name![..match.Index].Trim(' ', '-', ',');
        var label = Regex.Replace(entry.Name[(match.Index + match.Length)..], @"\s+", " ").Trim(' ', '-', ',');
        var kind = match.Groups["kind"].Value.ToLowerInvariant() switch
        {
            "ausstieg" or "sortie" or "uscita" => "Exit",
            _ => "Entry",
        };

        var site = sites
            .Where(s => s.Water.Slug == entry.Water!.Slug && Distance(s.Latitude, s.Longitude, entry.Latitude, entry.Longitude) < 400)
            .Where(s => baseName.Length == 0 || s.Name.StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => Distance(s.Latitude, s.Longitude, entry.Latitude, entry.Longitude))
            .FirstOrDefault();

        if (site is null)
        {
            site = new Site(baseName.Length > 0 ? baseName : entry.Name, entry.Latitude, entry.Longitude, entry.Water!, entry.Municipality, entry.Canton, [], []);
            sites.Add(site);
            review.Grouped.Add($"neuer Platz «{site.Name}» aus Einstiegen ({entry.Water!.NameDe})");
        }

        site.OsmIds.Add(entry.OsmId);
        site.EntryPoints.Add(new EntryPoint(kind, label.Length > 0 ? label : null, entry.Latitude, entry.Longitude));
        review.Grouped.Add($"«{entry.Name}» → Einstieg ({kind}) von «{site.Name}»");
    }

    foreach (var parking in parkings)
    {
        var site = sites
            .Where(s => Distance(s.Latitude, s.Longitude, parking.Latitude, parking.Longitude) < 400)
            .OrderBy(s => Distance(s.Latitude, s.Longitude, parking.Latitude, parking.Longitude))
            .FirstOrDefault();
        if (site is null)
        {
            review.Skipped.Add($"Parkplatz ohne Platz in 400 m: {parking.Name} ({Coordinates(parking)})");
            continue;
        }

        site.OsmIds.Add(parking.OsmId);
        site.EntryPoints.Add(new EntryPoint("Parking", parking.Name, parking.Latitude, parking.Longitude));
        review.Grouped.Add($"«{parking.Name}» → Parkplatz von «{site.Name}»");
    }

    return sites;
}

static JsonObject ToSeed(Site site, string waterSlug)
{
    var seed = new JsonObject
    {
        ["name"] = site.Name,
        ["waterBody"] = waterSlug,
        ["latitude"] = Math.Round(site.Latitude, 6),
        ["longitude"] = Math.Round(site.Longitude, 6),
        ["municipality"] = site.Municipality,
        ["canton"] = site.Canton,
        ["osm"] = new JsonArray([.. site.OsmIds.Select(id => JsonValue.Create(id))]),
    };

    if (site.EntryPoints.Count > 0)
    {
        seed["entryPoints"] = new JsonArray([.. site.EntryPoints.Select(e => new JsonObject
        {
            ["kind"] = e.Kind,
            ["name"] = e.Name,
            ["latitude"] = Math.Round(e.Latitude, 6),
            ["longitude"] = Math.Round(e.Longitude, 6),
        })]);
    }

    return seed;
}

static async Task WriteJson(string path, JsonArray array) =>
    await File.WriteAllTextAsync(path, array.ToJsonString(new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    }) + "\n");

static string Coordinates(Candidate c) => FormattableString.Invariant($"{c.Latitude:0.00000}, {c.Longitude:0.00000}");

static double Distance(double lat1, double lng1, double lat2, double lng2)
{
    const double EarthRadius = 6_371_000;
    var dLat = double.DegreesToRadians(lat2 - lat1);
    var dLng = double.DegreesToRadians(lng2 - lng1);
    var a = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(double.DegreesToRadians(lat1)) * Math.Cos(double.DegreesToRadians(lat2)) * Math.Pow(Math.Sin(dLng / 2), 2);
    return 2 * EarthRadius * Math.Asin(Math.Sqrt(a));
}

static class Osm
{
    public static (double Latitude, double Longitude) Position(JsonNode element) =>
        element["center"] is { } center
            ? ((double)center["lat"]!, (double)center["lon"]!)
            : ((double)element["lat"]!, (double)element["lon"]!);

    public static string? Tag(JsonObject? tags, string key) => (string?)tags?[key] is { Length: > 0 } value ? value.Trim() : null;

    /// <summary>Wie `Slugs.From` im Core.</summary>
    public static string Slug(string text)
    {
        var replaced = text.ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
        var plain = string.Concat(replaced.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));
        var slug = new StringBuilder();
        foreach (var character in plain)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                slug.Append(character);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        return slug.ToString().TrimEnd('-');
    }
}

static class Patterns
{
    public static readonly Regex EntryPoint = new(@"\b(?<kind>Einstieg|Ausstieg|Entrée|Sortie|Ingresso|Uscita)\b", RegexOptions.IgnoreCase);

    /// <summary>Seegebiete ohne Gemeinde, z.B. «Lago Maggiore (TI)» oder «Lac de Neuchâtel (NE)».</summary>
    public static readonly Regex LakeTerritory = new(@"^(Lac|Lago|Lai|Lej)\b|see \([A-Z]{2}\)$", RegexOptions.IgnoreCase);

    public static readonly Regex Parking = new(@"\b(Parkplatz|Parking|Parcheggio)\b", RegexOptions.IgnoreCase);
}

sealed record Candidate(string OsmId, string? Name, double Latitude, double Longitude, string? Source, Water? Water, string? Municipality, string? Canton);

sealed record EntryPoint(string Kind, string? Name, double Latitude, double Longitude);

sealed record Site(string Name, double Latitude, double Longitude, Water Water, string? Municipality, string? Canton, List<string> OsmIds, List<EntryPoint> EntryPoints);

sealed record Water(string NameDe, string? NameEn, string? NameFr, string? NameIt, string Type, double Latitude, double Longitude)
{
    private static readonly HashSet<string> Ignored = ["harbour", "basin", "wastewater", "reflecting_pool", "lock", "moat", "fountain"];

    private static readonly Regex IgnoredName = new(@"^(Port|Hafen|Alter Hafen|Bootshafen)\b", RegexOptions.IgnoreCase);

    /// <summary>Deutsche Namen aus OSM, die in der Schweiz niemand braucht.</summary>
    private static readonly Dictionary<string, string> CommonNames = new() { ["Langensee"] = "Lago Maggiore" };

    public string Slug => Osm.Slug(NameDe);

    /// <summary>Ein Gewässer aus OSM. Eine Fläche aus is_in hat keinen Mittelpunkt, dann gilt der Punkt des Platzes.</summary>
    public static Water? From(JsonObject tags, JsonNode element, double latitude, double longitude)
    {
        var name = Osm.Tag(tags, "name:de") ?? Osm.Tag(tags, "name");
        if (name is null || Ignored.Contains(Osm.Tag(tags, "water") ?? "") || IgnoredName.IsMatch(name))
        {
            return null;
        }

        var river = Osm.Tag(tags, "waterway") is not null || Osm.Tag(tags, "water") is "river" or "canal" or "stream";
        if (element["center"] is not null || element["lat"] is not null)
        {
            (latitude, longitude) = Osm.Position(element);
        }

        return new Water(CommonNames.GetValueOrDefault(name, name), Osm.Tag(tags, "name:en"), Osm.Tag(tags, "name:fr"), Osm.Tag(tags, "name:it"), river ? "River" : "Lake", latitude, longitude);
    }

    /// <summary>
    /// Zuerst das Gewässer, dessen Name im Namen des Platzes steht. Sonst geht ein See einem Fluss vor (Platz an einer Mündung),
    /// dann zählt der nächste Mittelpunkt.
    /// </summary>
    public static Water? Choose(List<Water> waters, Candidate site) => waters
        .OrderBy(w => site.Name?.Contains(w.NameDe, StringComparison.OrdinalIgnoreCase) == true ? 0 : 1)
        .ThenBy(w => w.Type == "Lake" ? 0 : 1)
        .ThenBy(w => Math.Pow(w.Latitude - site.Latitude, 2) + Math.Pow(w.Longitude - site.Longitude, 2))
        .FirstOrDefault();

}

sealed class NewWater(Water water)
{
    public Water Water { get; } = water;

    public List<Site> Sites { get; } = [];

    public JsonObject ToSeed()
    {
        var (latitude, longitude) = Water.Type == "River"
            ? (Sites.Average(s => s.Latitude), Sites.Average(s => s.Longitude))
            : (Water.Latitude, Water.Longitude);
        return new JsonObject
        {
            ["slug"] = Water.Slug,
            ["nameDe"] = Water.NameDe,
            ["nameEn"] = Water.NameEn,
            ["nameFr"] = Water.NameFr,
            ["nameIt"] = Water.NameIt,
            ["type"] = Water.Type,
            ["centroid"] = new JsonObject { ["latitude"] = Math.Round(latitude, 4), ["longitude"] = Math.Round(longitude, 4) },
        };
    }
}

sealed class Review
{
    public List<Site> Added { get; } = [];

    public List<string> Grouped { get; } = [];

    public List<string> Duplicates { get; } = [];

    public List<string> Skipped { get; } = [];

    public string Render(int total, IEnumerable<NewWater> newWaters)
    {
        var text = new StringBuilder();
        text.AppendLine("# OSM-Import zur Durchsicht").AppendLine();
        text.AppendLine($"{total} Punkte aus OSM, {Added.Count} neue Plätze. Daten © OpenStreetMap contributors, ODbL.").AppendLine();
        text.AppendLine("## Neue Gewässer").AppendLine();
        foreach (var water in newWaters.OrderBy(w => w.Water.NameDe))
        {
            text.AppendLine($"- {water.Water.NameDe} ({water.Water.Type}, {water.Sites.Count} Plätze) · fr {water.Water.NameFr ?? "–"} · it {water.Water.NameIt ?? "–"} · en {water.Water.NameEn ?? "–"}");
        }

        text.AppendLine().AppendLine("## Neue Plätze nach Gewässer").AppendLine();
        foreach (var group in Added.GroupBy(s => s.Water.NameDe).OrderBy(g => g.Key))
        {
            text.AppendLine($"### {group.Key} ({group.Count()})").AppendLine();
            foreach (var site in group)
            {
                var entries = site.EntryPoints.Count > 0 ? $" · {site.EntryPoints.Count} Einstiege" : "";
                text.AppendLine($"- {site.Name} · {site.Municipality} {site.Canton}{entries}");
            }

            text.AppendLine();
        }

        Section(text, "Einstiege und Parkplätze zugeordnet", Grouped);
        Section(text, "Doppelt (nicht übernommen)", Duplicates);
        Section(text, "Ausgelassen", Skipped);
        return text.ToString();
    }

    private static void Section(StringBuilder text, string title, List<string> lines)
    {
        text.AppendLine($"## {title} ({lines.Count})").AppendLine();
        lines.ForEach(line => text.AppendLine($"- {line}"));
        text.AppendLine();
    }
}
