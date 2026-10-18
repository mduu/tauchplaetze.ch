using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Geo;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.Forecasts.Queries;

/// <summary>Ein Ort mit Radius für die Rangliste.</summary>
public record Area(double Latitude, double Longitude, int RadiusKm);

/// <summary>
/// Die Rangliste eines Tages, bester Index zuerst. Nur Plätze mit Index (`forecast.md`, «Datenstufe»).
/// Ohne <paramref name="Area"/> gilt die ganze Schweiz (`features.md`). Ohne <paramref name="Date"/> gilt heute.
/// </summary>
public record GetBestSitesQuery(string Culture, DateOnly? Date = null, Area? Area = null);

/// <summary>Die verfügbaren Tage und die Rangliste des gewählten Tags.</summary>
public record BestSites(DateOnly Date, IReadOnlyList<DateOnly> Days, IReadOnlyList<RankedSite> Ranking);

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
    double? DistanceKm);

internal class GetBestSitesHandler(TauchplaetzeDbContext dbContext, TimeProvider time) : IQueryHandler<GetBestSitesQuery, BestSites>
{
    public async Task<BestSites> Handle(GetBestSitesQuery query, CancellationToken ct = default)
    {
        var today = SwissTime.Today(time);
        var days = Enumerable.Range(0, ForecastSources.ForecastDays).Select(today.AddDays).ToList();
        var date = query.Date is { } wanted && days.Contains(wanted) ? wanted : today;
        var origin = query.Area is { } area ? GeoPoints.Create(area.Latitude, area.Longitude) : null;

        var forecasts = dbContext.DailyForecasts
            .AsNoTracking()
            .Where(f => f.Date == date && f.DiveIndex != null)
            .Join(dbContext.DiveSites.Where(d => d.Status == DiveSiteStatus.Published), f => f.DiveSiteId, d => d.Id, (f, d) => new { f, d });

        if (origin is not null)
        {
            var meters = query.Area!.RadiusKm * 1000.0;
            forecasts = forecasts.Where(x => x.d.Location.IsWithinDistance(origin, meters));
        }

        var rows = await forecasts
            .OrderByDescending(x => x.f.DiveIndex)
            .Select(x => new
            {
                x.d.Slug,
                x.d.Name,
                x.d.Municipality,
                x.d.Location,
                Water = new { x.d.WaterBody!.NameDe, x.d.WaterBody.NameEn, x.d.WaterBody.NameFr, x.d.WaterBody.NameIt },
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

        var ranking = rows.Select(r => new RankedSite(
                r.Slug,
                r.Name,
                r.Municipality,
                WaterBodyNames.In(query.Culture, r.Water.NameDe, r.Water.NameEn, r.Water.NameFr, r.Water.NameIt),
                r.DiveIndex!.Value,
                r.DataLevel,
                r.SurfaceTemperature,
                r.TemperatureAt20m,
                r.ThermoclineTop,
                r.ThermoclineBottom,
                r.WindBeaufort,
                ReasonCodes.Parse(r.ReasonCodes),
                origin is null ? null : Math.Round(Distance.Km(origin, r.Location), 0)))
            .ToList();

        return new BestSites(date, days, ranking);
    }
}
