using System.Text.Json;
using Microsoft.Extensions.FileProviders;

namespace Tauchplaetze.Web.Features.DiveSites;

/// <summary>Ein Höhenraster für die 3D-Ansicht (Story 007): Datei, Ecke im Südwesten (LV95), Zahl der Punkte pro Seite und ihr Abstand in Metern.</summary>
public record SiteTerrain(string File, int East, int North, int Points, double Cell);

/// <summary>
/// Die Höhenraster der Plätze als statische Dateien (`wwwroot/terrain`, erzeugt von `tools/bathymetry/terrain.cs`).
/// Ein Raster gehört zu einem Platz, wenn dessen Koordinate höchstens <see cref="MaxDistanceMetres"/> von der Koordinate beim Erzeugen liegt.
/// </summary>
public sealed class SiteTerrainCatalog(IFileProvider webRoot, ILogger<SiteTerrainCatalog> logger)
{
    private const double MaxDistanceMetres = 60;

    private readonly Lazy<Index> index = new(() => Load(webRoot, logger));

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

    /// <remarks>Ein fehlender oder kaputter Index schaltet die 3D-Ansicht aus, ohne die Platzseite scheitern zu lassen.</remarks>
    private static Index Load(IFileProvider webRoot, ILogger logger)
    {
        var file = webRoot.GetFileInfo("terrain/index.json");
        if (!file.Exists)
        {
            return Index.Empty;
        }

        try
        {
            using var stream = file.CreateReadStream();
            var index = JsonSerializer.Deserialize<Index>(stream, JsonSerializerOptions.Web);
            return index is { Sites: not null, Points: > 1, CellSize: > 0 }
                ? index with { Sites = [.. index.Sites.Where(IsUsable)] }
                : Index.Empty;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Terrain index is unreadable. The 3D view is off.");
            return Index.Empty;
        }
    }

    private static bool IsUsable(Entry? entry) =>
        entry is { File.Length: > 0 } && double.IsFinite(entry.Lat) && double.IsFinite(entry.Lng);

    private sealed record Index(int Points, double CellSize, IReadOnlyList<Entry> Sites)
    {
        public static readonly Index Empty = new(0, 0, []);
    }

    private sealed record Entry(double Lat, double Lng, int E, int N, string? File);
}
