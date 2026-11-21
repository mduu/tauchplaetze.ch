using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Forecasts.Sources.Alplakes;
using Tauchplaetze.Core.Features.Forecasts.Sources.OpenMeteo;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.Echolot.Queries;

/// <summary>
/// Die Seite «Echolot» (`ux.md`, «Echolot»): der Stand jeder Datenquelle und die letzten <see cref="EcholotRules.MaxEntries"/> Neuigkeiten,
/// neueste zuerst. Neuigkeiten sind neue Funktionen aus `news.json` und Plätze mit erster Beschreibung, pro Tag gruppiert.
/// </summary>
public record GetEcholotQuery(string Culture);

internal class GetEcholotHandler(TauchplaetzeDbContext dbContext, TimeProvider time) : IQueryHandler<GetEcholotQuery, EcholotReport>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>So viele Plätze liest die Query höchstens für die Neuigkeiten. Reicht für <see cref="EcholotRules.MaxEntries"/> Tage.</summary>
    private const int DescribedSitesLimit = 500;

    public async Task<EcholotReport> Handle(GetEcholotQuery query, CancellationToken ct = default)
    {
        var sources = await Sources(ct);
        var entries = await Entries(query.Culture, ct);
        return new EcholotReport(sources, [.. entries.Take(EcholotRules.MaxEntries)], entries.Count > EcholotRules.MaxEntries);
    }

    private async Task<IReadOnlyList<DataSourceStatus>> Sources(CancellationToken ct)
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
            await Satellite(run, runHealth, calls.Where(c => c.Source == AlplakesClient.Source && IsSatellite(c)), ct),
            await Discharge(now, ct),
            await LakeLevel(now, ct),
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
    private async Task<DataSourceStatus> Satellite(ForecastRun? run, DataHealth runHealth, IEnumerable<SourceCallStatus> calls, CancellationToken ct)
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

    private async Task<DataSourceStatus> Discharge(DateTimeOffset now, CancellationToken ct)
    {
        var readings = await dbContext.HydroReadings.AsNoTracking().Select(r => r.MeasuredAt).ToListAsync(ct);
        return Measured(DataSource.Discharge, readings, now, EcholotRules.DischargeLateAfter);
    }

    private async Task<DataSourceStatus> LakeLevel(DateTimeOffset now, CancellationToken ct)
    {
        var readings = await dbContext.LakeLevelReadings.AsNoTracking().Select(r => r.MeasuredAt).ToListAsync(ct);
        return Measured(DataSource.LakeLevel, readings, now, EcholotRules.LakeLevelLateAfter);
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

    /// <summary>Am selben Tag steht eine neue Funktion vor den Plätzen.</summary>
    private async Task<List<EcholotEntry>> Entries(string culture, CancellationToken ct)
    {
        var features = EcholotNews.All.Select(n => Feature(n.Date, n.In(culture)));
        var described = await DescribedSites(culture, ct);
        return [.. features.Concat(described).OrderByDescending(e => e.Date).ThenBy(e => e.Kind)];
    }

    private static EcholotEntry Feature(DateOnly date, NewsText text) => new(date, EcholotEntryKind.Feature, text.Title, text.Text, []);

    private async Task<IEnumerable<EcholotEntry>> DescribedSites(string culture, CancellationToken ct)
    {
        var sites = await dbContext.DiveSites.AsNoTracking()
            .Where(s => s.Status == DiveSiteStatus.Published && s.FirstDescribedAt != null)
            .OrderByDescending(s => s.FirstDescribedAt)
            .Take(DescribedSitesLimit)
            .Select(s => new
            {
                s.Slug,
                s.Name,
                DescribedAt = s.FirstDescribedAt!.Value,
                s.WaterBody!.NameDe,
                s.WaterBody.NameEn,
                s.WaterBody.NameFr,
                s.WaterBody.NameIt,
            })
            .ToListAsync(ct);
        return sites
            .GroupBy(s => SwissTime.DayOf(s.DescribedAt))
            .Select(g => new EcholotEntry(g.Key, EcholotEntryKind.Described, null, null,
                [.. g.OrderBy(s => s.Name, StringComparer.InvariantCultureIgnoreCase)
                    .Select(s => new DescribedSite(s.Slug, s.Name, WaterBodyNames.In(culture, s.NameDe, s.NameEn, s.NameFr, s.NameIt)))]));
    }
}
