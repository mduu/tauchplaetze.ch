using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.DiveSites;

namespace Tauchplaetze.Core.Features.WaterBodies.Queries;

/// <summary>Alle aktiven Gewässer, sortiert nach dem Namen in der Sprache <paramref name="Culture"/> (`de`, `en`, `fr`, `it`). Zählt nur veröffentlichte Plätze.</summary>
public record GetWaterBodiesQuery(string Culture);

internal class GetWaterBodiesHandler(TauchplaetzeDbContext dbContext)
    : IQueryHandler<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>>
{
    public async Task<IReadOnlyList<WaterBodySummary>> Handle(GetWaterBodiesQuery query, CancellationToken ct = default)
    {
        var rows = await dbContext.WaterBodies
            .AsNoTracking()
            .Where(w => w.IsActive)
            .Select(w => new
            {
                w.Id, w.Slug, w.NameDe, w.NameEn, w.NameFr, w.NameIt, w.WaterBodyType, w.Centroid,
                SiteCount = dbContext.DiveSites.Count(d => d.WaterBodyId == w.Id && d.Status == DiveSiteStatus.Published),
            })
            .ToListAsync(ct);

        var comparer = StringComparer.Create(CultureInfo.GetCultureInfo(query.Culture), ignoreCase: true);

        return rows
            .Select(w => new WaterBodySummary(
                w.Id, w.Slug, WaterBodyNames.In(query.Culture, w.NameDe, w.NameEn, w.NameFr, w.NameIt), w.WaterBodyType, w.Centroid.Y, w.Centroid.X, w.SiteCount))
            .OrderBy(w => w.Name, comparer)
            .ToList();
    }
}
