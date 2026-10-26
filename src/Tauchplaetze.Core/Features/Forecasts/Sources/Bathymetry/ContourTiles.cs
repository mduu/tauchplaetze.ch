using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tauchplaetze.Core.Features.Forecasts.Sources.Bathymetry;

/// <summary>Ordner der Tiefenlinien aus swissBATHY3D (`data-sources.md`, «Tiefenlinien»). Ohne Ordner gibt es keine Richtung aus den Tiefenlinien.</summary>
public class BathymetryOptions
{
    public string? Directory { get; set; }
}

/// <summary>Liest die fertigen Kacheln der Tiefenlinien (`index.json` und `{see}/{kachel}.json`). Jede Kachel wird höchstens einmal gelesen.</summary>
internal class ContourTiles(IOptions<BathymetryOptions> options, ILogger<ContourTiles> logger)
{
    private const double MetersPerDegree = 111_320;

    private readonly Lazy<IReadOnlyDictionary<string, Dictionary<string, double[]>>> index = new(() => ReadIndex(options.Value.Directory, logger));

    private readonly ConcurrentDictionary<string, IReadOnlyList<(int Depth, IReadOnlyList<(double Latitude, double Longitude)> Line)>> tiles = [];

    /// <summary>Die Linien bei <paramref name="depth"/> m aus allen Kacheln, die näher als <paramref name="meters"/> am Punkt liegen. Leer ohne Tiefenlinien für den See.</summary>
    public IReadOnlyList<IReadOnlyList<(double Latitude, double Longitude)>> Near(string lake, double latitude, double longitude, int depth, double meters)
    {
        if (options.Value.Directory is not { } directory || !index.Value.TryGetValue(lake, out var lakeTiles))
        {
            return [];
        }

        var latitudeMargin = meters / MetersPerDegree;
        var longitudeMargin = meters / (MetersPerDegree * Math.Cos(double.DegreesToRadians(latitude)));
        return lakeTiles
            .Where(tile => tile.Value is [var west, var south, var east, var north]
                && longitude >= west - longitudeMargin && longitude <= east + longitudeMargin
                && latitude >= south - latitudeMargin && latitude <= north + latitudeMargin)
            .SelectMany(tile => tiles.GetOrAdd($"{lake}/{tile.Key}", key => ReadTile(Path.Combine(directory, $"{key}.json"))))
            .Where(line => line.Depth == depth)
            .Select(line => line.Line)
            .ToList();
    }

    private static IReadOnlyDictionary<string, Dictionary<string, double[]>> ReadIndex(string? directory, ILogger logger)
    {
        var path = directory is null ? null : Path.Combine(directory, "index.json");
        if (path is null || !File.Exists(path))
        {
            logger.LogWarning("Bathymetry index {Path} is missing, the forecast uses the satellite point for the direction to the lake", path);
            return new Dictionary<string, Dictionary<string, double[]>>();
        }

        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        return document.RootElement.GetProperty("lakes").EnumerateObject().ToDictionary(
            lake => lake.Name,
            lake => lake.Value.GetProperty("tiles").EnumerateObject().ToDictionary(
                tile => tile.Name,
                tile => tile.Value.EnumerateArray().Select(value => value.GetDouble()).ToArray()));
    }

    private static IReadOnlyList<(int Depth, IReadOnlyList<(double Latitude, double Longitude)> Line)> ReadTile(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        return document.RootElement.GetProperty("features").EnumerateArray()
            .SelectMany(feature => feature.GetProperty("geometry").GetProperty("coordinates").EnumerateArray()
                .Select(line => (
                    Depth: feature.GetProperty("properties").GetProperty("depth").GetInt32(),
                    Line: (IReadOnlyList<(double Latitude, double Longitude)>)line.EnumerateArray()
                        .Select(point => (point[1].GetDouble(), point[0].GetDouble()))
                        .ToList())))
            .ToList();
    }
}
