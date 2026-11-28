using System.Text.Json;

namespace Tauchplaetze.Web.Features.DiveSites;

/// <summary>Ein Höhenraster für die 3D-Ansicht (Story 007): Datei und Ecke im Südwesten (LV95), Punkte im Abstand <see cref="SiteTerrainCatalog.Cell"/>.</summary>
public record SiteTerrain(string File, int East, int North, int Points, double Cell);

/// <summary>
/// Die Höhenraster der Plätze als statische Dateien (`wwwroot/terrain`, erzeugt von `tools/bathymetry/terrain.cs`).
/// Ein Raster gehört zu einem Platz, wenn dessen Koordinate höchstens <see cref="MaxDistanceMetres"/> von der Koordinate beim Erzeugen liegt.
/// </summary>
public sealed class SiteTerrainCatalog(IWebHostEnvironment environment, ILogger<SiteTerrainCatalog> logger)
{
    public const double Cell = 2;

    private const double MaxDistanceMetres = 60;

    private readonly Lazy<Index> index = new(() => Load(environment, logger));

    /// <summary>Das Raster zum Platz an dieser Koordinate, sonst <c>null</c>.</summary>
    public SiteTerrain? Find(double latitude, double longitude)
    {
        var data = index.Value;
        var best = data.Sites
            .Select(s => (Site: s, Metres: Metres(latitude, longitude, s.Lat, s.Lng)))
            .Where(s => s.Metres <= MaxDistanceMetres)
            .OrderBy(s => s.Metres)
            .Select(s => s.Site)
            .FirstOrDefault();
        return best is null ? null : new SiteTerrain($"terrain/{best.File}", best.E, best.N, data.Points, data.CellSize);
    }

    private static double Metres(double lat1, double lng1, double lat2, double lng2)
    {
        var dy = (lat2 - lat1) * 111_320;
        var dx = (lng2 - lng1) * 111_320 * Math.Cos(lat1 * Math.PI / 180);
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static Index Load(IWebHostEnvironment environment, ILogger logger)
    {
        var file = environment.WebRootFileProvider.GetFileInfo("terrain/index.json");
        if (!file.Exists)
        {
            return new Index(0, Cell, []);
        }

        try
        {
            using var stream = file.CreateReadStream();
            return JsonSerializer.Deserialize<Index>(stream, JsonSerializerOptions.Web) ?? new Index(0, Cell, []);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Terrain index is unreadable. The 3D view is off.");
            return new Index(0, Cell, []);
        }
    }

    private sealed record Index(int Points, double CellSize, IReadOnlyList<Entry> Sites);

    private sealed record Entry(double Lat, double Lng, int E, int N, string File);
}
