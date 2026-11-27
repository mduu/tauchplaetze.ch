using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Shipping.Commands;

/// <summary>
/// Gleicht die Objekte der Schifffahrt mit `Seed/shipping-features.json` ab (`tools/shipping/`, `data-sources.md`, «Schifffahrt»).
/// Neue kommen dazu, geänderte werden nachgeführt, fehlende entfernt. Niemand bearbeitet sie von Hand.
/// </summary>
public record SeedShippingFeaturesCommand;

/// <summary>Zahl der angelegten, geänderten und entfernten Objekte.</summary>
public record SeedShippingFeaturesResult(int Added, int Updated, int Removed);

internal class SeedShippingFeaturesHandler(TauchplaetzeDbContext dbContext) : ICommandHandler<SeedShippingFeaturesCommand, SeedShippingFeaturesResult>
{
    private const string ResourceName = "Tauchplaetze.Core.Features.Shipping.Seed.shipping-features.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    private static readonly WKTReader Wkt = new(new NtsGeometryServices(PrecisionModel.Floating.Value, 4326));

    public async Task<SeedShippingFeaturesResult> Handle(SeedShippingFeaturesCommand command, CancellationToken ct = default)
    {
        var seeds = (await ReadSeed(ct)).ToDictionary(s => (s.Source, s.SourceKey));
        var existing = await dbContext.ShippingFeatures.ToListAsync(ct);
        var (added, updated, removed) = (0, 0, 0);

        foreach (var feature in existing)
        {
            if (!seeds.Remove((feature.Source, feature.SourceKey), out var seed))
            {
                dbContext.ShippingFeatures.Remove(feature);
                removed++;
                continue;
            }

            var geometry = GeometryOf(seed);
            if (feature.Kind != seed.Kind || feature.Name != seed.Name || !feature.Geometry.EqualsExact(geometry))
            {
                (feature.Kind, feature.Name, feature.Geometry) = (seed.Kind, seed.Name, geometry);
                updated++;
            }
        }

        foreach (var seed in seeds.Values)
        {
            dbContext.ShippingFeatures.Add(new ShippingFeature { Kind = seed.Kind, Name = seed.Name, Geometry = GeometryOf(seed), Source = seed.Source, SourceKey = seed.SourceKey });
            added++;
        }

        await dbContext.SaveChangesAsync(ct);
        return new SeedShippingFeaturesResult(added, updated, removed);
    }

    /// <summary>SQL Server verlangt bei `geography` den äusseren Ring gegen den Uhrzeigersinn. Sonst gälte die ganze Erde ausser dem Hafen.</summary>
    internal static Geometry GeometryOf(ShippingSeed seed)
    {
        var geometry = Wkt.Read(seed.Wkt);
        return geometry is Polygon polygon && !Orientation.IsCCW(polygon.Shell.CoordinateSequence)
            ? polygon.Factory.CreatePolygon((LinearRing)polygon.Shell.Reverse())
            : geometry;
    }

    private static async Task<List<ShippingSeed>> ReadSeed(CancellationToken ct)
    {
        await using var stream = typeof(SeedShippingFeaturesHandler).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Seed {ResourceName} fehlt.");
        return await JsonSerializer.DeserializeAsync<List<ShippingSeed>>(stream, JsonOptions, ct) ?? [];
    }
}

/// <summary>Ein Objekt im Seed, die Geometrie als WKT in WGS84 (Länge vor Breite).</summary>
internal record ShippingSeed(ShippingFeatureKind Kind, string? Name, ShippingSource Source, string SourceKey, string Wkt);
