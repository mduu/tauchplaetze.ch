using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.DiveSites;

namespace Tauchplaetze.Core.Features.Shipping.Queries;

/// <summary>Die Objekte der Schifffahrt für die Ebene «Schifffahrt» der Platzkarte (`stories/005-tauchen-und-schifffahrt.md`).</summary>
public record GetSiteShippingQuery(Guid DiveSiteId);

/// <summary>
/// Objekte im Umkreis von 500 m um den Platz und seine Einstiege. Leer: Die Ebene fehlt.
/// <see cref="NearLandingStage"/>: Name der Schiffstation, wenn der Platz oder ein Einstieg weniger als 100 m entfernt liegt.
/// </summary>
public record SiteShipping(IReadOnlyList<SiteShippingFeature> Features, string? NearLandingStage)
{
    public static readonly SiteShipping None = new([], null);
}

/// <summary>
/// Ein Objekt als Teile aus Punkten: Landungsstelle ein Punkt, Fahrlinie eine oder mehrere Linien (auf die Umgebung zugeschnitten), Hafen der äussere Ring.
/// <see cref="Centre"/>: Mitte für den Kreis um einen Hafen.
/// </summary>
public record SiteShippingFeature(ShippingFeatureKind Kind, string? Name, IReadOnlyList<IReadOnlyList<SitePoint>> Parts, SitePoint? Centre);

internal class GetSiteShippingHandler(TauchplaetzeDbContext dbContext) : IQueryHandler<GetSiteShippingQuery, SiteShipping>
{
    /// <summary>Fahrlinien sind oft so lang wie der See. Die Karte bekommt nur das Stück in diesem Umkreis.</summary>
    private const double ClipMeters = 1500;

    public async Task<SiteShipping> Handle(GetSiteShippingQuery query, CancellationToken ct = default)
    {
        var site = await dbContext.DiveSites
            .AsNoTracking()
            .Where(d => d.Id == query.DiveSiteId)
            .Select(d => new { d.Location, Entries = d.EntryPoints.Select(e => e.Location).ToList() })
            .SingleOrDefaultAsync(ct);
        if (site is null)
        {
            return SiteShipping.None;
        }

        var area = site.Location.Factory.CreateMultiPoint([site.Location, .. site.Entries]);
        var features = await dbContext.ShippingFeatures
            .AsNoTracking()
            .Where(f => f.Geometry.IsWithinDistance(area, ShippingRules.NearbyMeters))
            .Select(f => new
            {
                f.Kind,
                f.Name,
                f.Geometry,
                Distance = f.Geometry.Distance(area),
            })
            .ToListAsync(ct);

        var near = features
            .Where(f => f.Kind == ShippingFeatureKind.LandingStage && f.Distance < ShippingRules.LandingStageRadiusMeters)
            .OrderBy(f => f.Distance)
            .Select(f => f.Name)
            .FirstOrDefault();

        var clip = Clip(area);
        return new SiteShipping(
            [.. features
                .Select(f => (f.Kind, f.Name, Geometry: f.Kind == ShippingFeatureKind.FerryRoute ? f.Geometry.Intersection(clip) : f.Geometry))
                .Where(f => !f.Geometry.IsEmpty)
                .OrderBy(f => f.Kind)
                .Select(f => new SiteShippingFeature(f.Kind, f.Name, Parts(f.Geometry), f.Kind == ShippingFeatureKind.Harbour ? Point(f.Geometry.Centroid.Coordinate) : null))],
            near);
    }

    /// <summary>Rechteck rund um Platz und Einstiege, in Grad. Für das Zuschneiden genügt die Näherung.</summary>
    private static Geometry Clip(Geometry area)
    {
        var envelope = area.EnvelopeInternal.Copy();
        var latitude = ClipMeters / 111_200;
        envelope.ExpandBy(latitude / Math.Cos(envelope.Centre.Y * Math.PI / 180), latitude);
        return area.Factory.ToGeometry(envelope);
    }

    private static IReadOnlyList<IReadOnlyList<SitePoint>> Parts(Geometry geometry) => geometry switch
    {
        Polygon polygon => [Points(polygon.Shell)],
        GeometryCollection collection => [.. Enumerable.Range(0, collection.NumGeometries).SelectMany(i => Parts(collection.GetGeometryN(i)))],
        _ => [Points(geometry)],
    };

    private static IReadOnlyList<SitePoint> Points(Geometry geometry) => [.. geometry.Coordinates.Select(Point)];

    private static SitePoint Point(Coordinate coordinate) => new(coordinate.Y, coordinate.X);
}
