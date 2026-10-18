using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.DiveSites;

namespace Tauchplaetze.Core.Features.WaterBodies.Queries;

/// <summary>
/// Die aktiven Gewässer in Gruppen für die Übersicht: zuerst die grossen Seen, dann die übrigen Seen nach Grossregion,
/// dann die Flüsse, zuletzt Seen ohne Kanton. In jeder Gruppe nach Zahl der Plätze, dann nach Name in der Sprache <paramref name="Culture"/>.
/// </summary>
public record GetWaterBodyGroupsQuery(string Culture);

internal class GetWaterBodyGroupsHandler(TauchplaetzeDbContext dbContext, IQueryHandler<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>> waterBodies)
    : IQueryHandler<GetWaterBodyGroupsQuery, IReadOnlyList<WaterBodyGroup>>
{
    public async Task<IReadOnlyList<WaterBodyGroup>> Handle(GetWaterBodyGroupsQuery query, CancellationToken ct = default)
    {
        var waters = await waterBodies.Handle(new GetWaterBodiesQuery(query.Culture), ct);
        var regions = await RegionsByWaterBody(ct);
        var comparer = StringComparer.Create(CultureInfo.GetCultureInfo(query.Culture), ignoreCase: true);

        return waters
            .GroupBy(w => KeyOf(w, regions.GetValueOrDefault(w.Id)))
            .OrderBy(g => g.Key.Kind)
            .ThenBy(g => g.Key.Region)
            .Select(g => new WaterBodyGroup(g.Key.Kind, g.Key.Region, [.. g.OrderByDescending(w => w.SiteCount).ThenBy(w => w.Name, comparer)]))
            .ToList();
    }

    private static (WaterBodyGroupKind Kind, SwissRegion? Region) KeyOf(WaterBodySummary water, SwissRegion? region) => water switch
    {
        { Type: WaterBodyType.River } => (WaterBodyGroupKind.Rivers, null),
        { SiteCount: >= WaterBodyGroups.LargeLakeSites } => (WaterBodyGroupKind.LargeLakes, null),
        _ when region is not null => (WaterBodyGroupKind.Region, region),
        _ => (WaterBodyGroupKind.Other, null),
    };

    /// <summary>Die Region eines Gewässers ist die des Kantons mit den meisten veröffentlichten Plätzen. Gleichstand: Kantonskürzel alphabetisch.</summary>
    private async Task<Dictionary<int, SwissRegion?>> RegionsByWaterBody(CancellationToken ct)
    {
        var counts = await dbContext.DiveSites
            .AsNoTracking()
            .Where(d => d.Status == DiveSiteStatus.Published && d.Canton != null)
            .GroupBy(d => new { d.WaterBodyId, d.Canton })
            .Select(g => new { g.Key.WaterBodyId, g.Key.Canton, Sites = g.Count() })
            .ToListAsync(ct);

        return counts
            .Where(c => SwissRegions.Of(c.Canton) is not null)
            .GroupBy(c => c.WaterBodyId)
            .ToDictionary(
                g => g.Key,
                g => SwissRegions.Of(g.OrderByDescending(c => c.Sites).ThenBy(c => c.Canton!.Trim(), StringComparer.Ordinal).First().Canton));
    }
}
