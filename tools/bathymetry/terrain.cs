#:property PublishAot=false

// Rechnet pro Tauchplatz ein Höhenraster für die 3D-Ansicht (Story 007): Seegrund aus swissBATHY3D, Land aus swissALTI3D (beide swisstopo, OGD).
// Aufruf aus dem Repository: dotnet run tools/bathymetry/terrain.cs [-- --lake thunersee]
// Voraussetzung: die Tiefenlinien (contours.cs) sind gerechnet, ihr Index nennt die Kacheln der Seen und die Seehöhe.
// Ergebnis: src/Tauchplaetze.Web/wwwroot/terrain/index.json und {e}_{n}.bin. Cache der Downloads: tools/bathymetry/out/.
// Format und Regeln: specs/stories/007-platz-in-3d.md, «Daten», und specs/data-sources.md, «Höhenraster für die 3D-Ansicht».

using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

const string AltiStac = "https://data.geo.admin.ch/api/stac/v0.9/collections/ch.swisstopo.swissalti3d/items";
const string UserAgent = "tauchplaetze.ch terrain (https://tauchplaetze.ch)";
const int Cell = 2;
const int HalfSize = 120;
const int Points = HalfSize * 2 / Cell + 1;
const short NoData = short.MinValue;

var root = FindRepositoryRoot();
var cacheDir = Path.Combine(root, "tools", "bathymetry", "out");
var altiDir = Path.Combine(cacheDir, "alti");
var outDir = Path.Combine(root, "src", "Tauchplaetze.Web", "wwwroot", "terrain");
var onlyLake = Option("--lake");
Directory.CreateDirectory(altiDir);
Directory.CreateDirectory(outDir);

var bathymetry = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(root, "src", "Tauchplaetze.Web", "wwwroot", "bathymetry", "index.json")))!["lakes"]!.AsObject();
var lakeOfTile = new Dictionary<string, string>();
foreach (var (lake, info) in bathymetry)
{
    foreach (var (tile, _) in info!["tiles"]!.AsObject())
    {
        lakeOfTile[tile] = lake;
    }
}

var seed = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(root, "src", "Tauchplaetze.Core", "Features", "Editorial", "Seed", "dive-sites.json")))!.AsArray();
var blocks = new List<Block>();
foreach (var site in seed)
{
    var latitude = (double)site!["latitude"]!;
    var longitude = (double)site["longitude"]!;
    var (east, north) = Swiss.ToLv95(latitude, longitude);
    if (!lakeOfTile.TryGetValue($"{(int)Math.Floor(east / 1000)}_{(int)Math.Floor(north / 1000)}", out var lake) || (onlyLake is not null && lake != onlyLake))
    {
        continue;
    }

    var x0 = (int)Math.Round(east / Cell) * Cell - HalfSize;
    var y0 = (int)Math.Round(north / Cell) * Cell - HalfSize;
    if (blocks.Any(b => b.X0 == x0 && b.Y0 == y0))
    {
        continue;
    }

    blocks.Add(new Block((string)site["name"]!, lake, latitude, longitude, x0, y0, (double)bathymetry[lake]!["surface"]!));
}

Console.WriteLine($"{blocks.Count} Plätze an Seen mit swissBATHY3D");

foreach (var group in blocks.GroupBy(b => b.Lake))
{
    var zipPath = Path.Combine(cacheDir, $"{group.Key}.zip");
    if (!File.Exists(zipPath))
    {
        Console.WriteLine($"{group.Key}: Raster fehlt im Cache, zuerst contours.cs laufen lassen");
        continue;
    }

    Grids.ReadAsciiZip(zipPath, [.. group], (block, height) => block.AddLake(height));
    Console.WriteLine($"{group.Key}: Seegrund für {group.Count()} Plätze gelesen");
}

foreach (var tile in blocks.SelectMany(b => b.Tiles()).Distinct().OrderBy(t => t))
{
    var path = await AltiTile(tile.E, tile.N);
    if (path is null)
    {
        continue;
    }

    var touching = blocks.Where(b => b.Tiles().Contains(tile)).ToList();
    Grids.ReadXyzZip(path, touching);
}

var index = new JsonObject
{
    ["source"] = "swissBATHY3D, swissALTI3D, © swisstopo",
    ["cellSize"] = Cell,
    ["points"] = Points,
    ["format"] = "Int16 little-endian, Dezimeter über (+) oder unter (-) der Seehöhe, Zeile für Zeile von Süden nach Norden, je von Westen nach Osten",
};
var sites = new JsonArray();
var bytes = 0L;
foreach (var block in blocks.OrderBy(b => b.Lake).ThenBy(b => b.Name, StringComparer.Ordinal))
{
    if (!block.HasLake)
    {
        continue;
    }

    var data = block.Encode();
    var name = $"{block.X0}_{block.Y0}.bin";
    await File.WriteAllBytesAsync(Path.Combine(outDir, name), data);
    bytes += data.Length;
    sites.Add(new JsonObject
    {
        ["lat"] = Math.Round(block.Latitude, 6),
        ["lng"] = Math.Round(block.Longitude, 6),
        ["e"] = block.X0,
        ["n"] = block.Y0,
        ["surface"] = block.Surface,
        ["file"] = name,
    });
}

index["sites"] = sites;
await File.WriteAllTextAsync(Path.Combine(outDir, "index.json"), index.ToJsonString() + "\n");
Console.WriteLine($"{sites.Count} Raster, {bytes / 1024.0 / 1024.0:0.0} MB");
return;

async Task<string?> AltiTile(int e, int n)
{
    var cached = Directory.GetFiles(altiDir, $"*_{e}-{n}_2_2056_5728.xyz.zip").OrderDescending().FirstOrDefault();
    if (cached is not null)
    {
        return cached;
    }

    var (lon, lat) = Swiss.ToWgs84(e * 1000 + 500, n * 1000 + 500);
    var bbox = string.Create(CultureInfo.InvariantCulture, $"{lon - 0.0005},{lat - 0.0005},{lon + 0.0005},{lat + 0.0005}");
    await Task.Delay(600);
    var json = JsonNode.Parse(await Curl(["-s", "--fail", "--max-time", "60", $"{AltiStac}?bbox={bbox}"]))!;
    var href = json["features"]!.AsArray()
        .Where(f => ((string)f!["id"]!).EndsWith($"_{e}-{n}", StringComparison.Ordinal))
        .OrderByDescending(f => (string)f!["id"]!)
        .Select(f => f!["assets"]!.AsObject().FirstOrDefault(a => a.Key.EndsWith("_2_2056_5728.xyz.zip", StringComparison.Ordinal)).Value?["href"])
        .Select(h => (string?)h)
        .FirstOrDefault(h => h is not null);
    if (href is null)
    {
        Console.WriteLine($"swissALTI3D {e}-{n}: keine Kachel");
        return null;
    }

    var path = Path.Combine(altiDir, Path.GetFileName(new Uri(href).LocalPath));
    await Task.Delay(600);
    await Curl(["-s", "--fail", "--max-time", "600", "-o", path, href]);
    Console.WriteLine($"swissALTI3D {e}-{n}: geladen");
    return path;
}

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

// Über curl statt HttpClient, wie in contours.cs: .NET baut zu manchen Hosts keine TLS-Verbindung auf (macOS, 18.10.2026).
static async Task<string> Curl(string[] arguments)
{
    var start = new System.Diagnostics.ProcessStartInfo("curl", ["-A", UserAgent, .. arguments]) { RedirectStandardOutput = true };
    using var process = System.Diagnostics.Process.Start(start)!;
    var output = await process.StandardOutput.ReadToEndAsync();
    await process.WaitForExitAsync();
    return process.ExitCode == 0 ? output : throw new InvalidOperationException($"curl {process.ExitCode}: {string.Join(' ', arguments)}");
}

/// <summary>Der Ausschnitt eines Platzes: Punkte im Abstand von <c>Cell</c> Metern ab der Ecke <c>X0</c>, <c>Y0</c> (LV95, Südwesten).</summary>
sealed class Block(string name, string lake, double latitude, double longitude, int x0, int y0, double surface)
{
    private const int Cell = 2;
    private const int Points = 121;

    private readonly float[] lakeSum = new float[Points * Points];
    private readonly short[] lakeCount = new short[Points * Points];
    private readonly float[] land = new float[Points * Points];
    private readonly bool[] hasLand = new bool[Points * Points];

    public string Name => name;
    public string Lake => lake;
    public double Latitude => latitude;
    public double Longitude => longitude;
    public int X0 => x0;
    public int Y0 => y0;
    public double Surface => surface;
    public bool HasLake => lakeCount.Any(c => c > 0);

    public bool Contains(double east, double north) =>
        east >= x0 - Cell && east <= x0 + (Points - 1) * Cell + Cell && north >= y0 - Cell && north <= y0 + (Points - 1) * Cell + Cell;

    public IEnumerable<(int E, int N)> Tiles()
    {
        for (var e = (x0 - Cell) / 1000; e <= (x0 + Points * Cell) / 1000; e++)
        {
            for (var n = (y0 - Cell) / 1000; n <= (y0 + Points * Cell) / 1000; n++)
            {
                yield return (e, n);
            }
        }
    }

    public void AddLake((double East, double North, double Height) point)
    {
        if (Index(point.East, point.North) is { } i)
        {
            lakeSum[i] += (float)point.Height;
            lakeCount[i]++;
        }
    }

    public void SetLand(double east, double north, double height)
    {
        if (Index(east, north) is { } i)
        {
            land[i] = (float)height;
            hasLand[i] = true;
        }
    }

    /// <summary>Seegrund, wo swissBATHY3D Werte hat, sonst Land. Lücken füllt der Mittelwert der Nachbarn.</summary>
    public byte[] Encode()
    {
        var values = new short[Points * Points];
        for (var i = 0; i < values.Length; i++)
        {
            double? height = lakeCount[i] > 0 ? lakeSum[i] / lakeCount[i] : hasLand[i] ? land[i] : null;
            values[i] = height is { } h ? (short)Math.Clamp(Math.Round((h - surface) * 10), short.MinValue + 1, short.MaxValue) : short.MinValue;
        }

        Fill(values);
        var bytes = new byte[values.Length * 2];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private int? Index(double east, double north)
    {
        var col = (int)Math.Round((east - x0) / Cell);
        var row = (int)Math.Round((north - y0) / Cell);
        return col is >= 0 and < Points && row is >= 0 and < Points ? row * Points + col : null;
    }

    private static void Fill(short[] values)
    {
        for (var pass = 0; pass < Points && values.Contains(short.MinValue); pass++)
        {
            var next = (short[])values.Clone();
            for (var row = 0; row < Points; row++)
            {
                for (var col = 0; col < Points; col++)
                {
                    if (values[row * Points + col] != short.MinValue)
                    {
                        continue;
                    }

                    var neighbours = new[] { (row - 1, col), (row + 1, col), (row, col - 1), (row, col + 1) }
                        .Where(p => p.Item1 is >= 0 and < Points && p.Item2 is >= 0 and < Points)
                        .Select(p => values[p.Item1 * Points + p.Item2])
                        .Where(v => v != short.MinValue)
                        .ToList();
                    if (neighbours.Count > 0)
                    {
                        next[row * Points + col] = (short)neighbours.Average(v => v);
                    }
                }
            }

            Array.Copy(next, values, values.Length);
        }
    }
}

static class Grids
{
    /// <summary>Liest die ESRI-ASCII-Kacheln eines Sees, aber nur die, die einen Ausschnitt berühren.</summary>
    public static void ReadAsciiZip(string zipPath, IReadOnlyList<Block> blocks, Action<Block, (double, double, double)> add)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries.Where(e => e.Name.EndsWith(".asc", StringComparison.OrdinalIgnoreCase)))
        {
            using var reader = new StreamReader(entry.Open(), Encoding.ASCII, false, 1 << 20);
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
            var bottom = header["yllcorner"];
            var top = bottom + rows * size;
            var noData = header["nodata_value"];
            var touching = blocks.Where(b => b.X0 <= left + cols * size && b.X0 + 240 >= left && b.Y0 <= top && b.Y0 + 240 >= bottom).ToList();
            if (touching.Count == 0)
            {
                continue;
            }

            for (var row = 0; row < rows; row++)
            {
                var line = reader.ReadLine()!.AsSpan();
                var north = top - (row + 0.5) * size;
                if (!touching.Any(b => north >= b.Y0 - 2 && north <= b.Y0 + 242))
                {
                    continue;
                }

                var col = 0;
                foreach (var range in line.Split(' '))
                {
                    var token = line[range];
                    if (token.IsEmpty)
                    {
                        continue;
                    }

                    var east = left + (col + 0.5) * size;
                    col++;
                    var value = double.Parse(token, CultureInfo.InvariantCulture);
                    if (value == noData)
                    {
                        continue;
                    }

                    foreach (var block in touching)
                    {
                        if (block.Contains(east, north))
                        {
                            add(block, (east, north, value));
                        }
                    }
                }
            }
        }
    }

    /// <summary>Liest eine Kachel von swissALTI3D (XYZ, 2 m) und setzt die Höhen der Ausschnitte, die sie berührt.</summary>
    public static void ReadXyzZip(string zipPath, IReadOnlyList<Block> blocks)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries.Where(e => e.Name.EndsWith(".xyz", StringComparison.OrdinalIgnoreCase)))
        {
            using var reader = new StreamReader(entry.Open(), Encoding.ASCII, false, 1 << 20);
            while (reader.ReadLine() is { } line)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3 || !double.TryParse(parts[0], CultureInfo.InvariantCulture, out var east))
                {
                    continue;
                }

                var north = double.Parse(parts[1], CultureInfo.InvariantCulture);
                var height = double.Parse(parts[2], CultureInfo.InvariantCulture);
                foreach (var block in blocks)
                {
                    if (block.Contains(east, north))
                    {
                        block.SetLand(east, north, height);
                    }
                }
            }
        }
    }
}

/// <summary>Umrechnung zwischen WGS84 und LV95 mit den Näherungsformeln von swisstopo (Genauigkeit rund 1 m).</summary>
static class Swiss
{
    public static (double East, double North) ToLv95(double latitude, double longitude)
    {
        var phi = (latitude * 3600 - 169028.66) / 10000;
        var lambda = (longitude * 3600 - 26782.5) / 10000;
        var east = 2600072.37 + 211455.93 * lambda - 10938.51 * lambda * phi - 0.36 * lambda * phi * phi - 44.54 * lambda * lambda * lambda;
        var north = 1200147.07 + 308807.95 * phi + 3745.25 * lambda * lambda + 76.63 * phi * phi - 194.56 * lambda * lambda * phi + 119.79 * phi * phi * phi;
        return (east, north);
    }

    public static (double Longitude, double Latitude) ToWgs84(double east, double north)
    {
        var y = (east - 2_600_000) / 1_000_000;
        var x = (north - 1_200_000) / 1_000_000;
        var lambda = 2.6779094 + 4.728982 * y + 0.791484 * y * x + 0.1306 * y * x * x - 0.0436 * y * y * y;
        var phi = 16.9023892 + 3.238272 * x - 0.270978 * y * y - 0.002528 * x * x - 0.0447 * y * y * x - 0.0140 * x * x * x;
        return (lambda * 100 / 36, phi * 100 / 36);
    }
}

