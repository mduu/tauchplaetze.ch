using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Forecasts.Calculation;

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

        var days = rows.Select(f => (f, Thermocline: StoredThermocline.Read(f.ThermoclineTop, f.ThermoclineBottom)))
            .Select(t => new ForecastDay(
                t.f.Date,
                t.f.DataLevel,
                t.f.DiveIndex,
                t.f.ProfileSource,
                t.f.SurfaceTemperature,
                t.f.TemperatureAt10m,
                t.f.TemperatureAt20m,
                t.f.TemperatureAt30m,
                t.Thermocline is null ? null : t.f.ThermoclineTop,
                t.Thermocline is null ? null : t.f.ThermoclineBottom,
                t.f.SecchiDepth,
                t.f.SecchiObservedAt,
                t.f.VisibilityIndicator,
                t.Thermocline is null ? null : t.f.VisibilityGoodBelow,
                t.f.WindBeaufort,
                t.f.WindSpeedKmh,
                t.f.WindGustsKmh,
                t.f.PrecipitationLast72h,
                t.f.AlgaeLevel,
                StoredThermocline.Reasons(ReasonCodes.Parse(t.f.ReasonCodes), t.Thermocline),
                t.f.SecchiDepthShore,
                t.f.ClaritySource,
                t.f.CurrentSpeed,
                t.f.CurrentDirection,
                t.f.CurrentLevel))
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
