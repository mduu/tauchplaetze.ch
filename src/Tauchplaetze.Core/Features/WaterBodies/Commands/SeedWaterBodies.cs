using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Tauchplaetze.Core.CrossCutting.Geo;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.WaterBodies.Commands;

/// <summary>Legt die Gewässer aus `Seed/water-bodies.json` an, die noch fehlen. Bestehende Gewässer bleiben unverändert.</summary>
public record SeedWaterBodiesCommand;

/// <summary>Ergebnis des Seeds: Zahl der neu angelegten Gewässer.</summary>
public record SeedWaterBodiesResult(int Added);

internal class SeedWaterBodiesHandler(TauchplaetzeDbContext dbContext) : ICommandHandler<SeedWaterBodiesCommand, SeedWaterBodiesResult>
{
    private const string ResourceName = "Tauchplaetze.Core.Features.WaterBodies.Seed.water-bodies.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SeedWaterBodiesResult> Handle(SeedWaterBodiesCommand command, CancellationToken ct = default)
    {
        var existing = await dbContext.WaterBodies.Select(w => w.Slug).ToListAsync(ct);
        var missing = (await ReadSeed(ct)).Where(s => !existing.Contains(s.Slug)).ToList();

        dbContext.WaterBodies.AddRange(missing.Select(ToEntity));
        await dbContext.SaveChangesAsync(ct);

        return new SeedWaterBodiesResult(missing.Count);
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
        string? AlplakesSimstratKey);

    private sealed record Coordinate(double Latitude, double Longitude)
    {
        public Point ToPoint() => GeoPoints.Create(Latitude, Longitude);
    }
}
