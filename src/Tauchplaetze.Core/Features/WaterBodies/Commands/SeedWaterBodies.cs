using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Tauchplaetze.Core.CrossCutting.Geo;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.WaterBodies.Commands;

/// <summary>Legt die Gewässer und BAFU-Stationen aus `Seed/water-bodies.json` an, die noch fehlen. Bestehende bleiben unverändert.</summary>
public record SeedWaterBodiesCommand;

/// <summary>Ergebnis des Seeds: Zahl der neu angelegten Gewässer und Stationen.</summary>
public record SeedWaterBodiesResult(int Added, int StationsAdded = 0);

internal class SeedWaterBodiesHandler(TauchplaetzeDbContext dbContext) : ICommandHandler<SeedWaterBodiesCommand, SeedWaterBodiesResult>
{
    private const string ResourceName = "Tauchplaetze.Core.Features.WaterBodies.Seed.water-bodies.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SeedWaterBodiesResult> Handle(SeedWaterBodiesCommand command, CancellationToken ct = default)
    {
        var seeds = await ReadSeed(ct);
        var existing = await dbContext.WaterBodies.Include(w => w.HydroStations).ToDictionaryAsync(w => w.Slug, ct);
        var missing = seeds.Where(s => !existing.ContainsKey(s.Slug)).ToList();
        var missingStations = seeds
            .Where(s => existing.ContainsKey(s.Slug))
            .SelectMany(s => (s.HydroStations ?? []).Select(station => (WaterBody: existing[s.Slug], Station: station)))
            .Where(p => p.WaterBody.HydroStations.All(h => h.StationId != p.Station.Id))
            .ToList();

        dbContext.WaterBodies.AddRange(missing.Select(ToEntity));
        foreach (var (waterBody, station) in missingStations)
        {
            waterBody.HydroStations.Add(station.ToEntity());
        }

        await dbContext.SaveChangesAsync(ct);

        return new SeedWaterBodiesResult(missing.Count, missing.Sum(m => m.HydroStations?.Count ?? 0) + missingStations.Count);
    }

    private static async Task<IReadOnlyList<WaterBodySeed>> ReadSeed(CancellationToken ct)
    {
        await using var stream = typeof(SeedWaterBodiesHandler).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Ressource {ResourceName} fehlt.");
        return await JsonSerializer.DeserializeAsync<List<WaterBodySeed>>(stream, JsonOptions, ct) ?? [];
    }

    private static WaterBody ToEntity(WaterBodySeed seed) => new()
    {
        Slug = seed.Slug,
        NameDe = seed.NameDe,
        NameEn = seed.NameEn,
        NameFr = seed.NameFr,
        NameIt = seed.NameIt,
        WaterBodyType = Enum.Parse<WaterBodyType>(seed.Type),
        Centroid = seed.Centroid.ToPoint(),
        SatellitePoint = seed.SatellitePoint?.ToPoint(),
        AlplakesLakeKey = seed.AlplakesLakeKey,
        AlplakesModel3d = seed.AlplakesModel3d,
        AlplakesSimstratKey = seed.AlplakesSimstratKey,
        IsActive = true,
        HydroStations = [.. (seed.HydroStations ?? []).Select(s => s.ToEntity())],
    };

    private sealed record WaterBodySeed(
        string Slug,
        string NameDe,
        string? NameEn,
        string? NameFr,
        string? NameIt,
        string Type,
        Coordinate Centroid,
        Coordinate? SatellitePoint,
        string? AlplakesLakeKey,
        string? AlplakesModel3d,
        string? AlplakesSimstratKey,
        IReadOnlyList<HydroStationSeed>? HydroStations);

    private sealed record HydroStationSeed(string Id, string Name, string Role)
    {
        public WaterBodyHydroStation ToEntity() => new() { StationId = Id, Name = Name, Role = Enum.Parse<HydroStationRole>(Role) };
    }

    private sealed record Coordinate(double Latitude, double Longitude)
    {
        public Point ToPoint() => GeoPoints.Create(Latitude, Longitude);
    }
}
