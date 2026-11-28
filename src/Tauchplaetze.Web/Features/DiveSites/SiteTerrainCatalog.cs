using System.Text.Json;
using Microsoft.Extensions.FileProviders;

namespace Tauchplaetze.Web.Features.DiveSites;

/// <summary>Höhenraster der 3D-Ansicht (Story 007): Ecke im Südwesten in LV95, Abstand der Punkte in Metern.</summary>
public record SiteTerrain(string File, int East, int North, int Points, double Cell);

/// <summary>Die Höhenraster der Plätze als statische Dateien (`wwwroot/terrain`, erzeugt von `tools/bathymetry/terrain.cs`).</summary>
public sealed class SiteTerrainCatalog(IFileProvider webRoot, ILogger<SiteTerrainCatalog> logger)
{
    private const double MaxDistanceMetres = 60;

    private readonly Lazy<TerrainIndex> index = new(() => Load(webRoot, logger));

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

    private static TerrainIndex Load(IFileProvider webRoot, ILogger logger)
    {
        var file = webRoot.GetFileInfo("terrain/index.json");
        if (!file.Exists)
        {
            logger.LogWarning("Terrain index is missing. The 3D view is off.");
            return TerrainIndex.Empty;
        }

        try
        {
            using var stream = file.CreateReadStream();
            var loaded = JsonSerializer.Deserialize<TerrainIndex>(stream, JsonSerializerOptions.Web);
            if (loaded is not { Sites: not null, Points: > 1, CellSize: > 0 })
            {
                logger.LogWarning("Terrain index has no valid grid. The 3D view is off.");
                return TerrainIndex.Empty;
            }

            var usable = loaded.Sites.Where(entry => IsUsable(entry, webRoot)).ToList();
            if (usable.Count < loaded.Sites.Count)
            {
                logger.LogWarning("Terrain index has {Unusable} unusable entries. They are ignored.", loaded.Sites.Count - usable.Count);
            }

            return loaded with { Sites = usable };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Terrain index is unreadable. The 3D view is off.");
            return TerrainIndex.Empty;
        }
    }

    private static bool IsUsable(Entry? entry, IFileProvider webRoot) =>
        entry is { File.Length: > 0, E: > 0, N: > 0 }
        && double.IsFinite(entry.Lat)
        && double.IsFinite(entry.Lng)
        && webRoot.GetFileInfo($"terrain/{entry.File}").Exists;

    private sealed record TerrainIndex(int Points, double CellSize, IReadOnlyList<Entry> Sites)
    {
        public static readonly TerrainIndex Empty = new(0, 0, []);
    }

    private sealed record Entry(double Lat, double Lng, int E, int N, string? File);
}
