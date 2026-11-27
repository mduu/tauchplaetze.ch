#:property PublishAot=false

// Holt die Objekte der Schifffahrt für die Ebene «Schifffahrt» und schreibt den Seed der App
// (src/Tauchplaetze.Core/Features/Shipping/Seed/shipping-features.json). Regeln: specs/data-sources.md, «Schifffahrt».
// - Landungsstellen: BAV «öV-Haltestellen» (Opendata BY), Verkehrsmittel Schiff, Haltestelle, gültig. Pro Liegeplatz (Haltekante) ein Punkt, ohne Liegeplatz die Haltestelle.
// - Fahrlinien (route=ferry) und Häfen (leisure=marina): OpenStreetMap (ODbL).
// Nur Objekte im Umkreis von 1 km um einen Tauchplatz aus dem Seed.
// Aufruf aus dem Repository: dotnet run tools/shipping/shipping-features.cs [-- --cached]

using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

const string BavUrl = "https://data.geo.admin.ch/ch.bav.haltestellen-oev/haltestellen-oev/haltestellen-oev_2056_de.csv.zip";
const string Overpass = "https://overpass-api.de/api/interpreter";
const string UserAgent = "tauchplaetze.ch shipping-import (https://tauchplaetze.ch)";
const double NearSiteKm = 1.0;
const string Query = """
    [out:json][timeout:170];
    area["ISO3166-1"="CH"][admin_level=2]->.ch;
    (way["route"="ferry"](area.ch);nwr["leisure"="marina"](area.ch););
    out tags geom;
    """;

var root = FindRepositoryRoot();
var outDir = Path.Combine(root, "tools", "shipping", "out");
Directory.CreateDirectory(outDir);
var bavPath = Path.Combine(outDir, "haltestellen-oev.csv.zip");
var osmPath = Path.Combine(outDir, "overpass.json");
var cached = args.Contains("--cached");

if (!cached || !File.Exists(bavPath))
{
    await Curl(["-s", "-L", "--max-time", "240", "-A", UserAgent, "-o", bavPath, BavUrl]);
}

if (!cached || !File.Exists(osmPath))
{
    await File.WriteAllTextAsync(osmPath, await OverpassJson(Query));
}

var sites = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(root, "src", "Tauchplaetze.Core", "Features", "Editorial", "Seed", "dive-sites.json")))!.AsArray()
    .Select(s => new Point((double)s!["latitude"]!, (double)s["longitude"]!))
    .ToList();

var features = new List<Feature>();
features.AddRange(LandingStages(bavPath));
features.AddRange(OsmFeatures(JsonNode.Parse(await File.ReadAllTextAsync(osmPath))!["elements"]!.AsArray()));

var kept = features.Where(f => f.Parts.Any(part => sites.Any(site => Geo.DistanceKm(site, part) <= NearSiteKm))).ToList();
var seed = new JsonArray([.. kept
    .OrderBy(f => f.Kind).ThenBy(f => f.Source).ThenBy(f => f.SourceKey, StringComparer.Ordinal)
    .Select(f => new JsonObject
    {
        ["kind"] = f.Kind,
        ["name"] = f.Name,
        ["source"] = f.Source,
        ["sourceKey"] = f.SourceKey,
        ["wkt"] = f.Wkt,
    })]);

var seedPath = Path.Combine(root, "src", "Tauchplaetze.Core", "Features", "Shipping", "Seed", "shipping-features.json");
await File.WriteAllTextAsync(seedPath, seed.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
foreach (var group in kept.GroupBy(f => f.Kind))
{
    Console.WriteLine($"{group.Key}: {group.Count()} von {features.Count(f => f.Kind == group.Key)} im Umkreis von {NearSiteKm} km um einen Platz");
}

Console.WriteLine($"Seed: {seedPath}");
return;

static IEnumerable<Feature> LandingStages(string zipPath)
{
    using var zip = ZipFile.OpenRead(zipPath);
    var stops = Csv.Read(zip.Entries.Single(e => e.Name == "Betriebspunkt.csv"))
        .Where(r => r["Betriebspunkttyp_Code"] == "VP" && r["Verkehrsmittel_Bezeichnung"].Contains("Schiff", StringComparison.Ordinal) && r["Gueltigkeit_EndeGueltigkeit"].Length == 0)
        .ToDictionary(r => r["xtf_id"]);
    var berths = Csv.Read(zip.Entries.Single(e => e.Name == "Haltekante.csv"))
        .Where(r => stops.ContainsKey(r["rHaltestelle"]) && r["Gueltigkeit_EndeGueltigkeit"].Length == 0)
        .ToLookup(r => r["rHaltestelle"]);

    foreach (var stop in stops.Values)
    {
        var points = berths[stop["xtf_id"]].Select(b => (Key: b["Nummer"], Position: Geo.FromLv95(b["E"], b["N"]))).ToList();
        if (points.Count == 0)
        {
            points.Add((stop["Nummer"], Geo.FromLv95(stop["E"], stop["N"])));
        }

        foreach (var (key, position) in points)
        {
            yield return new Feature("LandingStage", stop["Name"], "Bav", key, [position], $"POINT ({Geo.Wkt(position)})");
        }
    }
}

static IEnumerable<Feature> OsmFeatures(JsonArray elements)
{
    foreach (var element in elements)
    {
        var tags = element!["tags"]?.AsObject();
        var key = $"{element["type"]}/{element["id"]}";
        var name = (string?)tags?["name"];
        if ((string?)tags?["route"] == "ferry" && element["geometry"] is JsonArray line && line.Count >= 2)
        {
            var points = line.Select(p => new Point((double)p!["lat"]!, (double)p["lon"]!)).ToList();
            yield return new Feature("FerryRoute", name, "OpenStreetMap", key, points, $"LINESTRING ({string.Join(", ", points.Select(Geo.Wkt))})");
        }
        else if ((string?)tags?["leisure"] == "marina" && Ring(element) is { Count: >= 4 } ring)
        {
            yield return new Feature("Harbour", name, "OpenStreetMap", key, ring, $"POLYGON (({string.Join(", ", ring.Select(Geo.Wkt))}))");
        }
    }
}

// Ein Hafen als Fläche: geschlossener Weg oder der längste geschlossene äussere Weg einer Relation. Häfen als Punkt fehlen, sie haben keine Fläche.
static List<Point>? Ring(JsonNode element)
{
    IEnumerable<JsonArray> candidates = element["geometry"] is JsonArray way
        ? [way]
        : (element["members"]?.AsArray() ?? []).Where(m => (string?)m!["role"] == "outer" && m["geometry"] is JsonArray).Select(m => m!["geometry"]!.AsArray());
    var ring = candidates
        .Select(g => g.Select(p => new Point((double)p!["lat"]!, (double)p["lon"]!)).ToList())
        .Where(points => points.Count >= 4 && points[0] == points[^1])
        .OrderByDescending(points => points.Count)
        .FirstOrDefault();
    return ring;
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

// Über curl statt HttpClient: .NET baut zu overpass-api.de keine TLS-Verbindung auf (macOS, 18.10.2026), curl schon.
static async Task<string> Curl(string[] arguments)
{
    var start = new System.Diagnostics.ProcessStartInfo("curl", arguments) { RedirectStandardOutput = true };
    using var process = System.Diagnostics.Process.Start(start)!;
    var body = await process.StandardOutput.ReadToEndAsync();
    await process.WaitForExitAsync();
    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"curl {process.ExitCode}");
    }

    return body;
}

static async Task<string> OverpassJson(string query)
{
    var delay = TimeSpan.FromSeconds(15);
    for (var attempt = 1; ; attempt++)
    {
        var body = await Curl(["-s", "--max-time", "240", "-A", UserAgent, Overpass, "--data-urlencode", $"data={query}"]);
        if (body.TrimStart().StartsWith('{') && !body.Contains("\"remark\": \"runtime error", StringComparison.Ordinal))
        {
            return body;
        }

        if (attempt == 6)
        {
            throw new InvalidOperationException($"Overpass antwortet nicht ({body.Split('\n').FirstOrDefault()?.Trim('#', ' ')}).");
        }

        Console.WriteLine($"Overpass: {body.Split('\n').FirstOrDefault()?.Trim('#', ' ')}, neuer Versuch in {delay.TotalSeconds} s.");
        await Task.Delay(delay);
        delay *= 2;
    }
}

sealed record Point(double Latitude, double Longitude);

sealed record Feature(string Kind, string? Name, string Source, string SourceKey, List<Point> Parts, string Wkt);

static class Geo
{
    public static double DistanceKm(Point a, Point b)
    {
        var dy = (a.Latitude - b.Latitude) * 111.2;
        var dx = (a.Longitude - b.Longitude) * 111.2 * Math.Cos(a.Latitude * Math.PI / 180);
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public static string Wkt(Point p) => string.Create(CultureInfo.InvariantCulture, $"{p.Longitude:0.######} {p.Latitude:0.######}");

    /// <summary>LV95 nach WGS84 mit den Näherungsformeln von swisstopo (Genauigkeit rund 1 m).</summary>
    public static Point FromLv95(string east, string north)
    {
        var y = (double.Parse(east, CultureInfo.InvariantCulture) - 2_600_000) / 1_000_000;
        var x = (double.Parse(north, CultureInfo.InvariantCulture) - 1_200_000) / 1_000_000;
        var longitude = 2.6779094 + 4.728982 * y + 0.791484 * y * x + 0.1306 * y * x * x - 0.0436 * y * y * y;
        var latitude = 16.9023892 + 3.238272 * x - 0.270978 * y * y - 0.002528 * x * x - 0.0447 * y * y * x - 0.0140 * x * x * x;
        return new Point(Math.Round(latitude * 100 / 36, 6), Math.Round(longitude * 100 / 36, 6));
    }
}

static class Csv
{
    public static IEnumerable<Dictionary<string, string>> Read(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        var header = Split(reader.ReadLine()!.TrimStart('﻿'));
        while (reader.ReadLine() is { } line)
        {
            var values = Split(line);
            yield return header.Select((name, i) => (name, value: i < values.Count ? values[i] : "")).ToDictionary(c => c.name, c => c.value);
        }
    }

    private static List<string> Split(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"' && quoted && i + 1 < line.Length && line[i + 1] == '"')
            {
                current.Append('"');
                i++;
            }
            else if (c == '"')
            {
                quoted = !quoted;
            }
            else if (c == ',' && !quoted)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        values.Add(current.ToString());
        return values;
    }
}
