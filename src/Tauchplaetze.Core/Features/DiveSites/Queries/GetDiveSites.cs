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
    NoticeKind? NoticeToday = null,
    SiteSymbols? Symbols = null)
{
    public SiteSymbols SymbolList => Symbols ?? SiteSymbols.None;
}

internal class GetDiveSitesHandler(TauchplaetzeDbContext dbContext, TimeProvider time) : IQueryHandler<GetDiveSitesQuery, IReadOnlyList<DiveSiteListItem>>
{
    public async Task<IReadOnlyList<DiveSiteListItem>> Handle(GetDiveSitesQuery query, CancellationToken ct = default)
    {
        var today = SwissTime.Today(time);
        var restrictions = await SiteRestrictions.Load(dbContext, ct);
        var sources = await SiteSymbolSources.Load(dbContext, ct);
        var rows = await dbContext.DiveSites
            .AsNoTracking()
            .Where(d => d.Status == DiveSiteStatus.Published)
            .Where(d => query.WaterBodySlug == null || d.WaterBody!.Slug == query.WaterBodySlug)
            .Select(d => new
            {
                d.Id,
                d.Slug,
                d.Name,
                d.Municipality,
                d.Location,
                d.ContentLevel,
                d.DataLevel,
                d.DifficultyRating,
                d.MaxDepth,
                d.WallRating,
                d.WrecksRating,
                d.Features,
                Water = new { d.WaterBody!.Slug, d.WaterBody.NameDe, d.WaterBody.NameEn, d.WaterBody.NameFr, d.WaterBody.NameIt, d.WaterBody.WaterBodyType, d.WaterBody.Elevation },
                Index = dbContext.DailyForecasts.Where(f => f.DiveSiteId == d.Id && f.Date == today).Select(f => f.DiveIndex).FirstOrDefault(),
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
                restrictions[r.Id].WorstOn(today),
                sources.For(r.Id, restrictions[r.Id].WorstOn(today), r.DifficultyRating, r.MaxDepth, r.WallRating, r.WrecksRating, r.Features, r.Water.WaterBodyType, r.Water.Elevation)))
            .OrderBy(s => s.WaterBodyName, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo(query.Culture), true))
            .ThenBy(s => s.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo(query.Culture), true))
            .ToList();
    }
}
