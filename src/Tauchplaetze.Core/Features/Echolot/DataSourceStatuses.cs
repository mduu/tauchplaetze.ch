using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Forecasts.Sources.Alplakes;
using Tauchplaetze.Core.Features.Forecasts.Sources.OpenMeteo;
using Tauchplaetze.Core.Features.Susv;

namespace Tauchplaetze.Core.Features.Echolot;

/// <summary>Liest den Stand jeder Datenquelle aus den eigenen Tabellen (`ux.md`, «Echolot»). Für die Seite und für den Gesamtzustand im Kopf.</summary>
internal static class DataSourceStatuses
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<IReadOnlyList<DataSourceStatus>> Read(TauchplaetzeDbContext dbContext, TimeProvider time, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var run = await dbContext.ForecastRuns.AsNoTracking()
            .Where(r => r.FinishedAt != null && (r.Status == ForecastRunStatus.Succeeded || r.Status == ForecastRunStatus.PartiallySucceeded))
            .OrderByDescending(r => r.FinishedAt)
            .FirstOrDefaultAsync(ct);
        var calls = run is null ? [] : JsonSerializer.Deserialize<List<SourceCallStatus>>(run.SourceStatus, JsonOptions) ?? [];
        var runHealth = run is null
            ? DataHealth.Unknown
            : EcholotRules.ByAge(now - run.FinishedAt!.Value, EcholotRules.ForecastLateAfter, EcholotRules.ForecastDownAfter);

        return
        [
            new DataSourceStatus(DataSource.Forecast, runHealth, run?.FinishedAt, run?.Sites),
            FromRun(DataSource.LakeModels, run, runHealth, calls.Where(c => c.Source == AlplakesClient.Source && !IsSatellite(c))),
            FromRun(DataSource.Weather, run, runHealth, calls.Where(c => c.Source == OpenMeteoClient.Source)),
            await Satellite(dbContext, time, run, runHealth, calls.Where(c => c.Source == AlplakesClient.Source && IsSatellite(c)), ct),
            await Discharge(dbContext, now, ct),
            await LakeLevel(dbContext, now, ct),
            await Susv(dbContext, now, ct),
        ];
    }

    private static bool IsSatellite(SourceCallStatus call) =>
        call.Key.StartsWith("satellite-", StringComparison.Ordinal) || call.Key.StartsWith("remotesensing", StringComparison.Ordinal);

    private static DataSourceStatus FromRun(DataSource source, ForecastRun? run, DataHealth runHealth, IEnumerable<SourceCallStatus> calls)
    {
        if (run is null)
        {
            return new DataSourceStatus(source, DataHealth.Unknown, null);
        }

        var list = calls.ToList();
        return new DataSourceStatus(source, EcholotRules.Worse(runHealth, EcholotRules.ByCalls(list.Count, list.Count(c => !c.Ok))), run.FinishedAt);
    }

    /// <summary>Wie die anderen Quellen des Laufs, dazu verspätet, wenn das neueste Bild älter als <see cref="EcholotRules.SatelliteLateAfterDays"/> Tage ist.</summary>
    private static async Task<DataSourceStatus> Satellite(TauchplaetzeDbContext dbContext, TimeProvider time, ForecastRun? run, DataHealth runHealth, IEnumerable<SourceCallStatus> calls, CancellationToken ct)
    {
        var status = FromRun(DataSource.Satellite, run, runHealth, calls);
        var observed = await dbContext.DailyForecasts.AsNoTracking().MaxAsync(f => f.SecchiObservedAt, ct);
        if (observed is not { } day)
        {
            return status;
        }

        var late = SwissTime.Today(time).DayNumber - day.DayNumber > EcholotRules.SatelliteLateAfterDays;
        return status with
        {
            ObservedOn = day,
            Health = late && status.Health == DataHealth.Current ? DataHealth.Late : status.Health,
        };
    }

    private static async Task<DataSourceStatus> Discharge(TauchplaetzeDbContext dbContext, DateTimeOffset now, CancellationToken ct)
    {
        var readings = await dbContext.HydroReadings.AsNoTracking().Select(r => r.MeasuredAt).ToListAsync(ct);
        return Measured(DataSource.Discharge, readings, now, EcholotRules.DischargeLateAfter);
    }

    private static async Task<DataSourceStatus> LakeLevel(TauchplaetzeDbContext dbContext, DateTimeOffset now, CancellationToken ct)
    {
        var readings = await dbContext.LakeLevelReadings.AsNoTracking().Select(r => r.MeasuredAt).ToListAsync(ct);
        return Measured(DataSource.LakeLevel, readings, now, EcholotRules.LakeLevelLateAfter);
    }

    /// <summary>
    /// Stand des SUSV: der älteste letzte Abgleich unter den Themen mit Einträgen. So zeigt auch ein einzelnes Thema, das nicht mehr liefert, «verspätet».
    /// <see cref="DataSourceStatus.Count"/>: bestätigte Einträge, die nicht entfernt sind.
    /// </summary>
    private static async Task<DataSourceStatus> Susv(TauchplaetzeDbContext dbContext, DateTimeOffset now, CancellationToken ct)
    {
        var topics = await dbContext.SusvEntries.AsNoTracking()
            .Where(e => e.RemovedAt == null)
            .GroupBy(e => e.Topic)
            .Select(g => g.Max(e => e.LastSeenAt))
            .ToListAsync(ct);
        if (topics.Count == 0)
        {
            return new DataSourceStatus(DataSource.Susv, DataHealth.Unknown, null);
        }

        var synced = topics.Min();
        var confirmed = await dbContext.SusvEntries.CountAsync(e => e.RemovedAt == null && e.ReviewStatus == SusvReviewStatus.Confirmed, ct);
        return new DataSourceStatus(DataSource.Susv, EcholotRules.ByAge(now - synced, EcholotRules.SusvLateAfter, EcholotRules.SusvDownAfter), synced, confirmed);
    }

    private static DataSourceStatus Measured(DataSource source, List<DateTimeOffset> measuredAt, DateTimeOffset now, TimeSpan lateAfter)
    {
        if (measuredAt.Count == 0)
        {
            return new DataSourceStatus(source, DataHealth.Unknown, null);
        }

        var latest = measuredAt.Max();
        return new DataSourceStatus(source, EcholotRules.ByAge(now - latest, lateAfter, EcholotRules.MeasurementDownAfter), latest, measuredAt.Count);
    }
}
