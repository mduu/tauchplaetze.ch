using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Geo;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;

namespace Tauchplaetze.Core.Features.DiveSites.Queries;

/// <summary>Öffentliche Plätze im Umkreis einer Koordinate, die nächsten zuerst. Warnt beim Vorschlag eines neuen Platzes vor Doppelten.</summary>
public record GetNearbyDiveSitesQuery(double Latitude, double Longitude, int RadiusMeters = 300);

/// <summary>Ein Platz in der Nähe mit Distanz in Metern.</summary>
public record NearbyDiveSite(string Slug, string Name, int DistanceMeters);

internal class GetNearbyDiveSitesHandler(TauchplaetzeDbContext dbContext) : IQueryHandler<GetNearbyDiveSitesQuery, IReadOnlyList<NearbyDiveSite>>
{
    public async Task<IReadOnlyList<NearbyDiveSite>> Handle(GetNearbyDiveSitesQuery query, CancellationToken ct = default)
    {
        var origin = GeoPoints.Create(query.Latitude, query.Longitude);
        var sites = await dbContext.DiveSites
            .AsNoTracking()
            .Where(d => d.Status == DiveSiteStatus.Published)
            .Select(d => new { d.Slug, d.Name, d.Location })
            .ToListAsync(ct);

        return [.. sites
            .Select(s => new NearbyDiveSite(s.Slug, s.Name, (int)Math.Round(Distance.Km(origin, s.Location) * 1000)))
            .Where(s => s.DistanceMeters <= query.RadiusMeters)
            .OrderBy(s => s.DistanceMeters)];
    }
}
