using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;

namespace Tauchplaetze.Core.Features.Forecasts.Queries;

/// <summary>Die Prognose eines Platzes von heute an (Lokalzeit).</summary>
public record GetSiteForecastQuery(Guid DiveSiteId);

internal class GetSiteForecastHandler(TauchplaetzeDbContext dbContext, TimeProvider time) : IQueryHandler<GetSiteForecastQuery, SiteForecast>
{
    public async Task<SiteForecast> Handle(GetSiteForecastQuery query, CancellationToken ct = default)
    {
        var today = SwissTime.Today(time);
        var rows = await dbContext.DailyForecasts
            .AsNoTracking()
            .Where(f => f.DiveSiteId == query.DiveSiteId && f.Date >= today)
            .OrderBy(f => f.Date)
            .ToListAsync(ct);

        var days = rows.Select(f => new ForecastDay(
                f.Date,
                f.DataLevel,
                f.DiveIndex,
                f.ProfileSource,
                f.SurfaceTemperature,
                f.TemperatureAt10m,
                f.TemperatureAt20m,
                f.TemperatureAt30m,
                f.ThermoclineTop,
                f.ThermoclineBottom,
                f.SecchiDepth,
                f.SecchiObservedAt,
                f.VisibilityIndicator,
                f.VisibilityGoodBelow,
                f.WindBeaufort,
                f.WindSpeedKmh,
                f.WindGustsKmh,
                f.PrecipitationLast72h,
                f.AlgaeLevel,
                ReasonCodes.Parse(f.ReasonCodes),
                f.SecchiDepthShore,
                f.ClaritySource,
                f.CurrentSpeed,
                f.CurrentDirection,
                f.CurrentLevel))
            .ToList();

        var waterBodyType = await dbContext.DiveSites
            .Where(s => s.Id == query.DiveSiteId)
            .Select(s => s.WaterBody!.WaterBodyType)
            .FirstOrDefaultAsync(ct);
        return new SiteForecast(days, rows.Count > 0 ? rows.Max(f => f.ComputedAt) : null, await Hours(query.DiveSiteId, today, ct), waterBodyType);
    }

    /// <remarks>Stunden vor heute 00:00 fallen weg. Stammt die Reihe von gestern, fehlt am Ende ein Tag.</remarks>
    private async Task<IReadOnlyList<WeatherHourForecast>> Hours(Guid diveSiteId, DateOnly today, CancellationToken ct)
    {
        var json = await dbContext.SiteWeatherOutlooks
            .AsNoTracking()
            .Where(o => o.DiveSiteId == diveSiteId)
            .Select(o => o.Hours)
            .SingleOrDefaultAsync(ct);
        if (json is null)
        {
            return [];
        }

        var start = today.ToDateTime(TimeOnly.MinValue);
        return (JsonSerializer.Deserialize<List<WeatherHourForecast>>(json, WeatherOutlookJson.Options) ?? [])
            .Where(h => h.LocalTime >= start)
            .ToList();
    }
}
