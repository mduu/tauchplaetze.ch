using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Tauchplaetze.Core.CrossCutting.Geo;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Forecasts.Calculation;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.Forecasts.Queries;

/// <summary>Ein Ort mit Radius für die Rangliste.</summary>
public record Area(double Latitude, double Longitude, int RadiusKm);

/// <summary>
/// Die Rangliste eines Tages nach <see cref="RankingRules"/>: bester Index zuerst, mit Ort abzüglich der Distanz, höchstens
/// <see cref="RankingRules.SitesPerWaterBody"/> Plätze pro Gewässer (die weiteren in <see cref="RankedSite.MoreList"/>).
/// Nur Plätze mit Index (`forecast.md`, «Datenstufe»), ohne Plätze, die an dem Tag gesperrt sind.
/// Ohne <paramref name="Area"/> gilt die ganze Schweiz (`features.md`). Ohne <paramref name="Date"/> gilt heute.
/// </summary>
public record GetBestSitesQuery(string Culture, DateOnly? Date = null, Area? Area = null);

/// <summary>Die verfügbaren Tage und die Rangliste des gewählten Tags.</summary>
/// <param name="Coverage">Gewässer mit Index an dem Tag, solange weniger als 80 % der Plätze einen Index haben, sonst <c>null</c>.</param>
/// <param name="WithoutForecast">Mit Ort: Plätze im Umkreis ohne Index, nächster zuerst, höchstens <see cref="GetBestSitesHandler.UnrankedLimit"/>.</param>
/// <param name="WithoutForecastCount">Mit Ort: Zahl aller Plätze im Umkreis ohne Index.</param>
public record BestSites(
    DateOnly Date,
    IReadOnlyList<DateOnly> Days,
    IReadOnlyList<RankedSite> Ranking,
    ForecastCoverage? Coverage = null,
    IReadOnlyList<UnrankedSite>? WithoutForecast = null,
    int WithoutForecastCount = 0);

/// <summary>Für welche Gewässer es schon eine Prognose gibt (`ux.md`, «Startseite»).</summary>
public record ForecastCoverage(IReadOnlyList<string> WaterBodyNames, int SitesWithIndex, int Sites);

/// <summary>Ein Platz im Umkreis ohne Index. Er erscheint unter der Rangliste, ohne Rang.</summary>
public record UnrankedSite(string Slug, string Name, string? Municipality, string WaterBodyName, double DistanceKm);

/// <summary>Ein Platz in der Rangliste mit den Werten für «Beste Wahl».</summary>
public record RankedSite(
    string Slug,
    string Name,
    string? Municipality,
    string WaterBodyName,
    decimal DiveIndex,
    byte DataLevel,
    decimal? SurfaceTemperature,
    decimal? TemperatureAt20m,
    decimal? ThermoclineTop,
    decimal? ThermoclineBottom,
    int? WindBeaufort,
    IReadOnlyList<ReasonCode> Reasons,
    double? DistanceKm,
    WaterBodyType WaterBodyType = WaterBodyType.Lake,
    int? MountainLakeElevation = null,
    IReadOnlyList<RankedSite>? More = null)
{
    /// <summary>Den Querschnitt mit Temperaturen gibt es nur im See.</summary>
    public bool HasDepthProfile => WaterBodyType != WaterBodyType.River;

    /// <summary>Beim zweiten Platz eines Gewässers: die weiteren Plätze dieses Gewässers in derselben Reihung.</summary>
    public IReadOnlyList<RankedSite> MoreList => More ?? [];
}

internal class GetBestSitesHandler(TauchplaetzeDbContext dbContext, TimeProvider time) : IQueryHandler<GetBestSitesQuery, BestSites>
{
    internal const int UnrankedLimit = 10;

    private const double FullCoverage = 0.8;

    public async Task<BestSites> Handle(GetBestSitesQuery query, CancellationToken ct = default)
    {
        var today = SwissTime.Today(time);
        var days = Enumerable.Range(0, ForecastSources.ForecastDays).Select(today.AddDays).ToList();
        var date = query.Date is { } wanted && days.Contains(wanted) ? wanted : today;
        var closed = NoticeRules.ClosureOn(date);
        var origin = query.Area is { } area ? GeoPoints.Create(area.Latitude, area.Longitude) : null;

        var forecasts = dbContext.DailyForecasts
            .AsNoTracking()
            .Where(f => f.Date == date && f.DiveIndex != null)
            .Join(dbContext.DiveSites.Where(d => d.Status == DiveSiteStatus.Published && !d.Notices.AsQueryable().Any(closed)), f => f.DiveSiteId, d => d.Id, (f, d) => new { f, d });

        if (origin is not null)
        {
            var meters = query.Area!.RadiusKm * 1000.0;
            forecasts = forecasts.Where(x => x.d.Location.IsWithinDistance(origin, meters));
        }

        var rows = await forecasts
            .Select(x => new
            {
                x.d.WaterBodyId,
                x.d.Slug,
                x.d.Name,
                x.d.Municipality,
                x.d.Location,
                Water = new { x.d.WaterBody!.NameDe, x.d.WaterBody.NameEn, x.d.WaterBody.NameFr, x.d.WaterBody.NameIt, x.d.WaterBody.WaterBodyType, x.d.WaterBody.Elevation },
                x.f.DiveIndex,
                x.f.DataLevel,
                x.f.SurfaceTemperature,
                x.f.TemperatureAt20m,
                x.f.ThermoclineTop,
                x.f.ThermoclineBottom,
                x.f.WindBeaufort,
                x.f.ReasonCodes,
            })
            .ToListAsync(ct);

        var sites = rows.Select(r => (r, Thermocline: StoredThermocline.Read(r.ThermoclineTop, r.ThermoclineBottom)))
            .Select(t => (t.r.WaterBodyId, Site: new RankedSite(
                t.r.Slug,
                t.r.Name,
                t.r.Municipality,
                WaterBodyNames.In(query.Culture, t.r.Water.NameDe, t.r.Water.NameEn, t.r.Water.NameFr, t.r.Water.NameIt),
                t.r.DiveIndex!.Value,
                t.r.DataLevel,
                t.r.SurfaceTemperature,
                t.r.TemperatureAt20m,
                t.Thermocline is null ? null : t.r.ThermoclineTop,
                t.Thermocline is null ? null : t.r.ThermoclineBottom,
                t.r.WindBeaufort,
                StoredThermocline.Reasons(ReasonCodes.Parse(t.r.ReasonCodes), t.Thermocline),
                origin is null ? null : Math.Round(Distance.Km(origin, t.r.Location), 0),
                t.r.Water.WaterBodyType,
                MountainLakes.Elevation(t.r.Water.WaterBodyType, t.r.Water.Elevation))))
            .OrderByDescending(s => RankingRules.Score(s.Site.DiveIndex, s.Site.DistanceKm))
            .ThenByDescending(s => s.Site.DiveIndex)
            .ThenBy(s => s.Site.Name, StringComparer.Ordinal)
            .ToList();
        var ranking = Spread(sites);

        var coverage = await Coverage(query.Culture, date, ct);
        var (withoutForecast, withoutForecastCount) = origin is null ? ([], 0) : await Unranked(query, date, origin, ct);
        return new BestSites(date, days, ranking, coverage, withoutForecast, withoutForecastCount);
    }

    private static List<RankedSite> Spread(IReadOnlyList<(int WaterBodyId, RankedSite Site)> sites)
    {
        var more = sites
            .GroupBy(s => s.WaterBodyId)
            .ToDictionary(g => g.Key, g => g.Skip(RankingRules.SitesPerWaterBody).Select(s => s.Site).ToList());
        var shown = new Dictionary<int, int>();
        var ranking = new List<RankedSite>();
        foreach (var (waterBodyId, site) in sites)
        {
            var count = shown.GetValueOrDefault(waterBodyId) + 1;
            shown[waterBodyId] = count;
            if (count < RankingRules.SitesPerWaterBody)
            {
                ranking.Add(site);
            }
            else if (count == RankingRules.SitesPerWaterBody)
            {
                ranking.Add(more[waterBodyId] is [_, ..] rest ? site with { More = rest } : site);
            }
        }

        return ranking;
    }

    private async Task<ForecastCoverage?> Coverage(string culture, DateOnly date, CancellationToken ct)
    {
        var published = dbContext.DiveSites.AsNoTracking().Where(d => d.Status == DiveSiteStatus.Published);
        var sites = await published.CountAsync(ct);
        var withIndex = published.Where(d => dbContext.DailyForecasts.Any(f => f.DiveSiteId == d.Id && f.Date == date && f.DiveIndex != null));
        var sitesWithIndex = await withIndex.CountAsync(ct);
        if (sites == 0 || sitesWithIndex >= sites * FullCoverage)
        {
            return null;
        }

        var waters = await withIndex
            .Select(d => new { d.WaterBody!.NameDe, d.WaterBody.NameEn, d.WaterBody.NameFr, d.WaterBody.NameIt })
            .Distinct()
            .ToListAsync(ct);
        var names = waters
            .Select(w => WaterBodyNames.In(culture, w.NameDe, w.NameEn, w.NameFr, w.NameIt))
            .Order(StringComparer.Create(new CultureInfo(culture), ignoreCase: true))
            .ToList();
        return new ForecastCoverage(names, sitesWithIndex, sites);
    }

    private async Task<(IReadOnlyList<UnrankedSite> Sites, int Count)> Unranked(GetBestSitesQuery query, DateOnly date, Point origin, CancellationToken ct)
    {
        var meters = query.Area!.RadiusKm * 1000.0;
        var unranked = dbContext.DiveSites
            .AsNoTracking()
            .Where(d => d.Status == DiveSiteStatus.Published && d.Location.IsWithinDistance(origin, meters))
            .Where(d => !dbContext.DailyForecasts.Any(f => f.DiveSiteId == d.Id && f.Date == date && f.DiveIndex != null));
        var count = await unranked.CountAsync(ct);
        var rows = await unranked
            .OrderBy(d => d.Location.Distance(origin))
            .Take(UnrankedLimit)
            .Select(d => new { d.Slug, d.Name, d.Municipality, d.Location, Water = new { d.WaterBody!.NameDe, d.WaterBody.NameEn, d.WaterBody.NameFr, d.WaterBody.NameIt } })
            .ToListAsync(ct);
        var sites = rows
            .Select(r => new UnrankedSite(
                r.Slug,
                r.Name,
                r.Municipality,
                WaterBodyNames.In(query.Culture, r.Water.NameDe, r.Water.NameEn, r.Water.NameFr, r.Water.NameIt),
                Math.Round(Distance.Km(origin, r.Location), 0)))
            .ToList();
        return (sites, count);
    }
}
