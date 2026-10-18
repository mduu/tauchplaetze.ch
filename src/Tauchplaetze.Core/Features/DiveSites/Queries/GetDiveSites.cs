using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.DiveSites.Queries;

/// <summary>Alle öffentlichen Plätze, optional nur eines Gewässers, mit dem Index von heute. Sortiert nach Gewässer und Name.</summary>
public record GetDiveSitesQuery(string Culture, string? WaterBodySlug = null);

/// <summary>Ein Platz im Verzeichnis und auf der Karte. <see cref="NoticeToday"/>: der schwerste heute gültige Hinweis.</summary>
public record DiveSiteListItem(
    string Slug,
    string Name,
    string? Municipality,
    string WaterBodySlug,
    string WaterBodyName,
    WaterBodyType WaterBodyType,
    double Latitude,
    double Longitude,
    byte ContentLevel,
    byte? DataLevel,
    decimal? DiveIndexToday,
    NoticeKind? NoticeToday = null);

internal class GetDiveSitesHandler(TauchplaetzeDbContext dbContext, TimeProvider time) : IQueryHandler<GetDiveSitesQuery, IReadOnlyList<DiveSiteListItem>>
{
    public async Task<IReadOnlyList<DiveSiteListItem>> Handle(GetDiveSitesQuery query, CancellationToken ct = default)
    {
        var today = SwissTime.Today(time);
        var active = NoticeRules.ActiveOn(today);
        var rows = await dbContext.DiveSites
            .AsNoTracking()
            .Where(d => d.Status == DiveSiteStatus.Published)
            .Where(d => query.WaterBodySlug == null || d.WaterBody!.Slug == query.WaterBodySlug)
            .Select(d => new
            {
                d.Slug,
                d.Name,
                d.Municipality,
                d.Location,
                d.ContentLevel,
                d.DataLevel,
                Water = new { d.WaterBody!.Slug, d.WaterBody.NameDe, d.WaterBody.NameEn, d.WaterBody.NameFr, d.WaterBody.NameIt, d.WaterBody.WaterBodyType },
                Index = dbContext.DailyForecasts.Where(f => f.DiveSiteId == d.Id && f.Date == today).Select(f => f.DiveIndex).FirstOrDefault(),
                Notices = d.Notices.AsQueryable().Where(active).Select(n => n.Kind).ToList(),
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new DiveSiteListItem(
                r.Slug,
                r.Name,
                r.Municipality,
                r.Water.Slug,
                WaterBodyNames.In(query.Culture, r.Water.NameDe, r.Water.NameEn, r.Water.NameFr, r.Water.NameIt),
                r.Water.WaterBodyType,
                r.Location.Y,
                r.Location.X,
                r.ContentLevel,
                r.DataLevel,
                r.Index,
                r.Notices.Count == 0 ? null : r.Notices.Max()))
            .OrderBy(s => s.WaterBodyName, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo(query.Culture), true))
            .ThenBy(s => s.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo(query.Culture), true))
            .ToList();
    }
}
