using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Forecasts.Calculation;

namespace Tauchplaetze.Core.Features.Forecasts.Queries;

/// <summary>Der Abfluss an einem Flussplatz: neuester Messwert, Einordnung, Trend, 48 Stunden und 7 Tage. <c>null</c> an einem See, ohne Station oder ohne Messwert.</summary>
public record GetSiteDischargeQuery(Guid DiveSiteId);

internal class GetSiteDischargeHandler(TauchplaetzeDbContext dbContext, TimeProvider time) : IQueryHandler<GetSiteDischargeQuery, SiteDischarge?>
{
    /// <summary>Ein älterer Messwert gilt als veraltet. Der Import läuft alle 30 Minuten, das BAFU liefert alle 10 Minuten.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(2);

    private const int CurveHours = 48;

    private const int MeanDays = 7;

    public async Task<SiteDischarge?> Handle(GetSiteDischargeQuery query, CancellationToken ct = default)
    {
        var site = await dbContext.DiveSites
            .AsNoTracking()
            .Include(s => s.WaterBody).ThenInclude(w => w!.HydroStations)
            .SingleOrDefaultAsync(s => s.Id == query.DiveSiteId, ct);
        if (site is null || DischargeStations.For(site.Location, site.InflowStationOverride, site.WaterBody!) is not { } stationId)
        {
            return null;
        }

        var reading = await dbContext.HydroReadings.AsNoTracking().SingleOrDefaultAsync(r => r.StationId == stationId, ct);
        if (reading is null)
        {
            return null;
        }

        var now = time.GetUtcNow();
        var today = SwissTime.Today(time);
        var since = SwissTime.ToUtc(today.AddDays(1 - MeanDays), 0);
        var hours = await dbContext.HydroHours
            .AsNoTracking()
            .Where(h => h.StationId == stationId && h.Hour >= since)
            .OrderBy(h => h.Hour)
            .ToListAsync(ct);
        var measured = hours.Where(h => h.Kind == HydroSeriesKind.Measured).ToList();

        var siteThresholds = site is { DischargeLowFrom: { } low, DischargeMediumFrom: { } medium, DischargeStrongFrom: { } strong }
            ? new DischargeThresholds(low, medium, strong)
            : null;
        var thresholds = DischargeRules.ThresholdsFor(siteThresholds, reading.Median30Days);
        var curveStart = DischargeRules.StartOfHour(now).AddHours(1 - CurveHours);

        return new SiteDischarge(
            stationId,
            site.WaterBody!.HydroStations.FirstOrDefault(s => s.StationId == stationId)?.Name ?? stationId,
            reading.Discharge,
            reading.MeasuredAt,
            now - reading.MeasuredAt > StaleAfter,
            thresholds is null ? null : DischargeRules.LevelOf(reading.Discharge, thresholds),
            thresholds,
            siteThresholds is not null,
            reading.Median30Days,
            reading.DischargeThreeHoursBefore is { } before ? DischargeRules.TrendOf(reading.Discharge, before) : null,
            reading.Discharge - reading.DischargeThreeHoursBefore,
            [.. measured.Where(h => h.Hour >= curveStart).Select(h => new DischargeHour(h.Hour, h.Discharge))],
            [.. measured
                .GroupBy(h => SwissTime.DayOf(h.Hour))
                .Select(g => new DischargeDay(g.Key, Math.Round(g.Average(h => h.Discharge), 1)))],
            [.. hours.Where(h => h.Kind == HydroSeriesKind.Forecast && h.Hour >= now).Select(h => new DischargeHour(h.Hour, h.Discharge))]);
    }
}
