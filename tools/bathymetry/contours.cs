#:property PublishAot=false

// Rechnet Tiefenlinien aus swissBATHY3D (swisstopo, OGD, «© swisstopo») als kleine GeoJSON-Kacheln für Karte und Tauchskizze.
// Aufruf aus dem Repository: dotnet run tools/bathymetry/contours.cs [-- --lake zuerichsee]
// Ergebnis: src/Tauchplaetze.Web/wwwroot/bathymetry/index.json und {see}/{e}_{n}.json. Cache der Downloads: tools/bathymetry/out/.
// Ablauf und Regeln: specs/data-sources.md, «swisstopo», und specs/dive-site.md, «Tauchskizze».

using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

const string Stac = "https://data.geo.admin.ch/api/stac/v0.9/collections/ch.swisstopo.swissbathy3d/items?limit=100";
const string UserAgent = "tauchplaetze.ch bathymetry (https://tauchplaetze.ch)";
const int Cell = 5;
const int TileCells = 1000 / Cell;
const double Tolerance = 1.5;
const double MinLength = 30;
int[] depths = [5, 10, 15, 20, 25, 30, 35, 40, 50, 60];

var root = FindRepositoryRoot();
var cacheDir = Path.Combine(root, "tools", "bathymetry", "out");
var outDir = Path.Combine(root, "src", "Tauchplaetze.Web", "wwwroot", "bathymetry");
var onlyLake = Option("--lake");
Directory.CreateDirectory(cacheDir);
Directory.CreateDirectory(outDir);

var indexPath = Path.Combine(outDir, "index.json");
var index = File.Exists(indexPath) ? JsonNode.Parse(await File.ReadAllTextAsync(indexPath))!.AsObject() : new JsonObject();
var lakes = index["lakes"]?.AsObject() ?? new JsonObject();
index["source"] = "swissBATHY3D, © swisstopo";
index["cellSize"] = Cell;
index["depths"] = new JsonArray([.. depths.Select(d => JsonValue.Create(d))]);

foreach (var (slug, zipUrl, date) in await LakeList())
{
    if (onlyLake is not null && slug != onlyLake)
    {
        continue;
    }

    var zipPath = Path.Combine(cacheDir, $"{slug}.zip");
    if (!File.Exists(zipPath))
    {
        Console.WriteLine($"{slug}: lade {zipUrl}");
        await Curl(["-s", "--fail", "--max-time", "3600", "-o", zipPath, zipUrl]);
    }

    var grid = BlockGrid.Read(zipPath);
    var surface = LakeLevels.Mean.TryGetValue(slug, out var level) ? level : grid.Max;
    var lakeDir = Path.Combine(outDir, slug);
    if (Directory.Exists(lakeDir))
    {
        Directory.Delete(lakeDir, true);
    }

    Directory.CreateDirectory(lakeDir);

    var tiles = new JsonObject();
    var bytes = 0L;
    foreach (var tile in grid.Tiles.Keys.OrderBy(t => t.E).ThenBy(t => t.N))
    {
        var features = new JsonArray();
        foreach (var depth in depths)
        {
            var lines = Contours.Trace(grid.TileWindow(tile, surface), surface - depth)
                .Select(line => line.Select(p => ((tile.E * TileCells + p.X + 0.5) * Cell, (tile.N * TileCells + p.Y + 0.5) * Cell)).ToList())
                .Select(line => Lines.Simplify(line, Tolerance))
                .Where(line => Lines.Length(line) >= MinLength)
                .ToList();
            if (lines.Count == 0)
            {
                continue;
            }

            features.Add(new JsonObject
            {
                ["type"] = "Feature",
                ["properties"] = new JsonObject { ["depth"] = depth },
                ["geometry"] = new JsonObject
                {
                    ["type"] = "MultiLineString",
                    ["coordinates"] = new JsonArray([.. lines.Select(line => (JsonNode)new JsonArray([.. line.Select(Swiss.ToWgs84Json)]))]),
                },
            });
        }

        if (features.Count == 0)
        {
            continue;
        }

        var name = $"{tile.E}_{tile.N}";
        var json = new JsonObject { ["type"] = "FeatureCollection", ["features"] = features }.ToJsonString();
        await File.WriteAllTextAsync(Path.Combine(lakeDir, $"{name}.json"), json);
        bytes += Encoding.UTF8.GetByteCount(json);
        var (w, s) = Swiss.ToWgs84(tile.E * 1000.0, tile.N * 1000.0);
        var (e, n) = Swiss.ToWgs84(tile.E * 1000.0 + 1000, tile.N * 1000.0 + 1000);
        tiles[name] = new JsonArray(Round(w), Round(s), Round(e), Round(n));
    }

    var bounds = tiles.Select(t => t.Value!.AsArray().Select(v => (double)v!).ToArray()).ToList();
    lakes[slug] = new JsonObject
    {
        ["surface"] = Math.Round(surface, 1),
        ["date"] = date,
        ["bbox"] = new JsonArray(Round(bounds.Min(b => b[0])), Round(bounds.Min(b => b[1])), Round(bounds.Max(b => b[2])), Round(bounds.Max(b => b[3]))),
        ["tiles"] = tiles,
    };
    Console.WriteLine($"{slug}: Seespiegel {surface:0.0} m ü. M. (höchster Rasterwert {grid.Max:0.0}, 99.9-Perzentil {grid.Percentile999():0.0}), {tiles.Count} Kacheln, {bytes / 1024.0:0} KB");
}

index["lakes"] = lakes;
await File.WriteAllTextAsync(indexPath, index.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) + "\n");
return;

static double Round(double value) => Math.Round(value, 6);

string? Option(string name)
{
    var position = Array.IndexOf(args, name);
    return position >= 0 && position + 1 < args.Length ? args[position + 1] : null;
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

static async Task<List<(string Slug, string Url, string? Date)>> LakeList()
{
    var json = await Curl(["-s", "--fail", "--max-time", "120", Stac]);
    return JsonNode.Parse(json)!["features"]!.AsArray()
        .Select(f => (
            Slug: ((string)f!["id"]!).Replace("swissbathy3d_", ""),
            Url: (string)f["assets"]!.AsObject().First(a => a.Key.EndsWith(".esriasciigrid.zip", StringComparison.Ordinal)).Value!["href"]!,
            Date: (string?)f["properties"]?["datetime"]))
        .ToList();
}

// Über curl statt HttpClient, wie im OSM-Import: .NET baut zu manchen Hosts keine TLS-Verbindung auf (macOS, 18.10.2026).
static async Task<string> Curl(string[] arguments)
{
    var start = new System.Diagnostics.ProcessStartInfo("curl", ["-A", UserAgent, .. arguments]) { RedirectStandardOutput = true };
    using var process = System.Diagnostics.Process.Start(start)!;
    var output = await process.StandardOutput.ReadToEndAsync();
    await process.WaitForExitAsync();
    return process.ExitCode == 0 ? output : throw new InvalidOperationException($"curl {process.ExitCode}: {string.Join(' ', arguments)}");
}

readonly record struct TileKey(int E, int N);

/// <summary>
/// Mittlere Seehöhe in m ü. M. (LN02, gerundet, Quelle: BAFU-Pegel und Landeskarte). swissBATHY3D reicht nicht ganz bis ans Ufer:
/// Der höchste Rasterwert liegt beim Zürichsee 1.3 m unter dem Wasserspiegel. Darum gilt diese Tabelle, ohne Eintrag der höchste Rasterwert.
/// Für Linien im Abstand von 5 m genügt diese Genauigkeit. Stauseen schwanken stärker (Lungerersee).
/// </summary>
static class LakeLevels
{
    public static readonly Dictionary<string, double> Mean = new()
    {
        ["aegerisee"] = 723.9,
        ["baldeggersee"] = 463.0,
        ["bielersee"] = 429.3,
        ["bodensee"] = 395.3,
        ["brienzersee"] = 563.7,
        ["hallwilersee"] = 448.7,
        ["lacdejoux"] = 1004.0,
        ["lacleman"] = 372.0,
        ["lacneuchatel"] = 429.3,
        ["lagomaggiore"] = 193.5,
        ["lungernsee"] = 688.0,
        ["murtensee"] = 429.3,
        ["rotsee"] = 419.0,
        ["sarnersee"] = 468.6,
        ["sempachersee"] = 503.8,
        ["silsersee"] = 1797.0,
        ["silvaplanersee"] = 1790.5,
        ["thunersee"] = 557.8,
        ["vierwaldstaettersee"] = 433.6,
        ["walensee"] = 419.0,
        ["zuerichsee"] = 405.9,
        ["zugersee"] = 413.6,
    };
}

/// <summary>Das Raster eines Sees, gemittelt auf Blöcke von 5 m × 5 m, abgelegt in Kacheln von 1 km.</summary>
sealed class BlockGrid
{
    private const int TileCells = 200;

    public Dictionary<TileKey, (float[] Sum, short[] Count)> Tiles { get; } = [];

    public double Max { get; private set; } = double.MinValue;

    private readonly int[] histogram = new int[30000];

    public static BlockGrid Read(string zipPath)
    {
        var grid = new BlockGrid();
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries.Where(e => e.Name.EndsWith(".asc", StringComparison.OrdinalIgnoreCase)))
        {
            using var reader = new StreamReader(entry.Open(), Encoding.ASCII, false, 1 << 20);
            grid.Add(reader);
        }

        return grid;
    }

    private void Add(StreamReader reader)
    {
        var header = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < 6; i++)
        {
            var parts = reader.ReadLine()!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            header[parts[0]] = double.Parse(parts[1], CultureInfo.InvariantCulture);
        }

        var cols = (int)header["ncols"];
        var rows = (int)header["nrows"];
        var size = header["cellsize"];
        var left = header["xllcorner"];
        var top = header["yllcorner"] + rows * size;
        var noData = header["nodata_value"];

        for (var row = 0; row < rows; row++)
        {
            var line = reader.ReadLine()!.AsSpan();
            var col = 0;
            foreach (var range in line.Split(' '))
            {
                var token = line[range];
                if (token.IsEmpty)
                {
                    continue;
                }

                var value = double.Parse(token, CultureInfo.InvariantCulture);
                if (value != noData)
                {
                    Put(left + (col + 0.5) * size, top - (row + 0.5) * size, value);
                }

                col++;
                if (col == cols)
                {
                    break;
                }
            }
        }
    }

    private void Put(double east, double north, double height)
    {
        var bx = (int)Math.Floor(east / 5);
        var by = (int)Math.Floor(north / 5);
        var key = new TileKey(Math.DivRem(bx, TileCells).Quotient, Math.DivRem(by, TileCells).Quotient);
        if (!Tiles.TryGetValue(key, out var tile))
        {
            tile = (new float[TileCells * TileCells], new short[TileCells * TileCells]);
            Tiles[key] = tile;
        }

        var index = (by - key.N * TileCells) * TileCells + (bx - key.E * TileCells);
        tile.Sum[index] += (float)height;
        tile.Count[index]++;
        Max = Math.Max(Max, height);
        histogram[Math.Clamp((int)(height * 10) - 0, 0, histogram.Length - 1)]++;
    }

    /// <summary>Das 99.9-Perzentil der Höhen, zur Kontrolle neben der mittleren Seehöhe.</summary>
    public double Percentile999()
    {
        var total = histogram.Sum(h => (long)h);
        var target = total * 0.999;
        long seen = 0;
        for (var i = 0; i < histogram.Length; i++)
        {
            seen += histogram[i];
            if (seen >= target)
            {
                return i / 10.0;
            }
        }

        return Max;
    }

    /// <summary>
    /// Das Fenster einer Kachel mit einer Zeile und Spalte der Nachbarn im Osten und Norden, damit die Linien an der Grenze anschliessen.
    /// Ohne Daten gilt der Seespiegel (Ufer).
    /// </summary>
    public double[,] TileWindow(TileKey key, double surface)
    {
        var window = new double[TileCells + 1, TileCells + 1];
        for (var x = 0; x <= TileCells; x++)
        {
            for (var y = 0; y <= TileCells; y++)
            {
                window[x, y] = HeightAt(key.E * TileCells + x, key.N * TileCells + y) ?? surface;
            }
        }

        return window;
    }

    private double? HeightAt(int bx, int by)
    {
        var key = new TileKey(Math.DivRem(bx, TileCells).Quotient, Math.DivRem(by, TileCells).Quotient);
        if (!Tiles.TryGetValue(key, out var tile))
        {
            return null;
        }

        var index = (by - key.N * TileCells) * TileCells + (bx - key.E * TileCells);
        return tile.Count[index] > 0 ? tile.Sum[index] / tile.Count[index] : null;
    }
}

/// <summary>Marching Squares auf einem Raster mit Höhen. Ergebnis: Linien in Rasterkoordinaten, verbunden über gemeinsame Kanten.</summary>
static class Contours
{
    public static List<List<(double X, double Y)>> Trace(double[,] grid, double level)
    {
        var width = grid.GetLength(0);
        var height = grid.GetLength(1);
        var segments = new List<(long A, long B)>();
        var points = new Dictionary<long, (double X, double Y)>();

        for (var x = 0; x < width - 1; x++)
        {
            for (var y = 0; y < height - 1; y++)
            {
                var v0 = grid[x, y];
                var v1 = grid[x + 1, y];
                var v2 = grid[x + 1, y + 1];
                var v3 = grid[x, y + 1];
                var code = (v0 < level ? 1 : 0) | (v1 < level ? 2 : 0) | (v2 < level ? 4 : 0) | (v3 < level ? 8 : 0);
                if (code is 0 or 15)
                {
                    continue;
                }

                long Bottom() => Edge(x, y, 0, width, points, (x + Fraction(v0, v1, level), y));
                long Right() => Edge(x + 1, y, 1, width, points, (x + 1, y + Fraction(v1, v2, level)));
                long Top() => Edge(x, y + 1, 0, width, points, (x + Fraction(v3, v2, level), y + 1));
                long Left() => Edge(x, y, 1, width, points, (x, y + Fraction(v0, v3, level)));

                var center = (v0 + v1 + v2 + v3) / 4 < level;
                switch (code)
                {
                    case 1 or 14: segments.Add((Left(), Bottom())); break;
                    case 2 or 13: segments.Add((Bottom(), Right())); break;
                    case 3 or 12: segments.Add((Left(), Right())); break;
                    case 4 or 11: segments.Add((Right(), Top())); break;
                    case 6 or 9: segments.Add((Bottom(), Top())); break;
                    case 7 or 8: segments.Add((Left(), Top())); break;
                    case 5:
                        segments.AddRange(center ? [(Left(), Top()), (Bottom(), Right())] : [(Left(), Bottom()), (Right(), Top())]);
                        break;
                    case 10:
                        segments.AddRange(center ? [(Left(), Bottom()), (Right(), Top())] : [(Left(), Top()), (Bottom(), Right())]);
                        break;
                }
            }
        }

        return Join(segments, points);
    }

    private static double Fraction(double a, double b, double level) => Math.Abs(b - a) < 1e-9 ? 0.5 : Math.Clamp((level - a) / (b - a), 0, 1);

    private static long Edge(int x, int y, int vertical, int width, Dictionary<long, (double X, double Y)> points, (double X, double Y) point)
    {
        var key = ((long)y * width + x) * 2 + vertical;
        points.TryAdd(key, point);
        return key;
    }

    private static List<List<(double X, double Y)>> Join(List<(long A, long B)> segments, Dictionary<long, (double X, double Y)> points)
    {
        var byEdge = new Dictionary<long, List<int>>();
        for (var i = 0; i < segments.Count; i++)
        {
            Link(byEdge, segments[i].A, i);
            Link(byEdge, segments[i].B, i);
        }

        var used = new bool[segments.Count];
        var lines = new List<List<(double X, double Y)>>();
        for (var start = 0; start < segments.Count; start++)
        {
            if (used[start])
            {
                continue;
            }

            used[start] = true;
            var chain = new LinkedList<long>([segments[start].A, segments[start].B]);
            Extend(chain, byEdge, segments, used, atEnd: true);
            Extend(chain, byEdge, segments, used, atEnd: false);
            lines.Add([.. chain.Select(k => points[k])]);
        }

        return lines;
    }

    private static void Link(Dictionary<long, List<int>> byEdge, long edge, int segment)
    {
        if (!byEdge.TryGetValue(edge, out var list))
        {
            byEdge[edge] = list = [];
        }

        list.Add(segment);
    }

    private static void Extend(LinkedList<long> chain, Dictionary<long, List<int>> byEdge, List<(long A, long B)> segments, bool[] used, bool atEnd)
    {
        while (true)
        {
            var edge = atEnd ? chain.Last!.Value : chain.First!.Value;
            var next = byEdge[edge].FirstOrDefault(s => !used[s], -1);
            if (next < 0)
            {
                return;
            }

            used[next] = true;
            var other = segments[next].A == edge ? segments[next].B : segments[next].A;
            if (atEnd)
            {
                chain.AddLast(other);
            }
            else
            {
                chain.AddFirst(other);
            }
        }
    }
}

static class Lines
{
    public static double Length(List<(double X, double Y)> line) =>
        line.Zip(line.Skip(1), (a, b) => Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2))).Sum();

    /// <summary>Douglas-Peucker in Metern (LV95).</summary>
    public static List<(double X, double Y)> Simplify(List<(double X, double Y)> line, double tolerance)
    {
        if (line.Count < 3)
        {
            return line;
        }

        var keep = new bool[line.Count];
        keep[0] = keep[^1] = true;
        var stack = new Stack<(int First, int Last)>();
        stack.Push((0, line.Count - 1));
        while (stack.Count > 0)
        {
            var (first, last) = stack.Pop();
            var (index, distance) = (-1, 0.0);
            for (var i = first + 1; i < last; i++)
            {
                var d = DistanceToSegment(line[i], line[first], line[last]);
                if (d > distance)
                {
                    (index, distance) = (i, d);
                }
            }

            if (index >= 0 && distance > tolerance)
            {
                keep[index] = true;
                stack.Push((first, index));
                stack.Push((index, last));
            }
        }

        return [.. line.Where((_, i) => keep[i])];
    }

    private static double DistanceToSegment((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
    {
        var (dx, dy) = (b.X - a.X, b.Y - a.Y);
        var lengthSquared = dx * dx + dy * dy;
        var t = lengthSquared == 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared, 0, 1);
        return Math.Sqrt(Math.Pow(p.X - (a.X + t * dx), 2) + Math.Pow(p.Y - (a.Y + t * dy), 2));
    }
}

/// <summary>LV95 nach WGS84 mit den Näherungsformeln von swisstopo (Genauigkeit rund 1 m).</summary>
static class Swiss
{
    public static (double Longitude, double Latitude) ToWgs84(double east, double north)
    {
        var y = (east - 2_600_000) / 1_000_000;
        var x = (north - 1_200_000) / 1_000_000;
        var lambda = 2.6779094 + 4.728982 * y + 0.791484 * y * x + 0.1306 * y * x * x - 0.0436 * y * y * y;
        var phi = 16.9023892 + 3.238272 * x - 0.270978 * y * y - 0.002528 * x * x - 0.0447 * y * y * x - 0.0140 * x * x * x;
        return (lambda * 100 / 36, phi * 100 / 36);
    }

    public static JsonNode ToWgs84Json((double X, double Y) point)
    {
        var (longitude, latitude) = ToWgs84(point.X, point.Y);
        return new JsonArray(Math.Round(longitude, 6), Math.Round(latitude, 6));
    }
}
