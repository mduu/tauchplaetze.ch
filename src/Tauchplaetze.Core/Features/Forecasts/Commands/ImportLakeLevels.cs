using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Locks;
using Tauchplaetze.Core.CrossCutting.Storage;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Forecasts.Sources.Hydro;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.Forecasts.Commands;

/// <summary>
/// Holt den Pegel der Seestationen aller Seen mit Plätzen in einem Call und speichert den neuesten Wert pro Station (`forecast.md`, «Seepegel»).
/// Einmal pro Tag holt er dazu pro Station die Statistik des BAFU für den Kalendertag. Fällt das BAFU aus (5xx, Timeout), versucht es der nächste Lauf wieder.
/// </summary>
public record ImportLakeLevelsCommand;

/// <summary>
/// Ergebnis: Stationen mit Pegel und Stationen ohne Pegel. <see cref="Failed"/>: Der Call scheiterte. <see cref="Skipped"/>: Ein anderer Lauf hielt den Lock.
/// <see cref="Norms"/>: Stationen mit neuer Statistik in diesem Lauf.
/// </summary>
public record ImportLakeLevelsResult(int Stations, int StationsWithoutValues, bool Failed = false, bool Skipped = false, int Norms = 0);

internal class ImportLakeLevelsHandler(
    TauchplaetzeDbContext dbContext,
    HydroClient hydro,
    BafuStatisticsClient statistics,
    IRawDataStore rawData,
    IRunLock runLock,
    TimeProvider time)
    : ICommandHandler<ImportLakeLevelsCommand, ImportLakeLevelsResult>
{
    public const string LockName = "lake-level";

    /// <summary>Höchstens 2 Calls pro Sekunde an das BAFU (`data-sources.md`, «Höflich»).</summary>
    private static readonly TimeSpan BetweenCalls = TimeSpan.FromMilliseconds(500);

    public async Task<ImportLakeLevelsResult> Handle(ImportLakeLevelsCommand command, CancellationToken ct = default)
    {
        await using var acquired = await runLock.TryAcquire(LockName, ct);
        if (acquired is null)
        {
            return new ImportLakeLevelsResult(0, 0, Skipped: true);
        }

        var stations = await StationsToImport(ct);
        if (stations.Count == 0)
        {
            return new ImportLakeLevelsResult(0, 0);
        }

        var now = time.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var recorder = new SourceRecorder(rawData, $"lake-level/{now:yyyy-MM-dd}/{now:HHmm}");
        var heights = await recorder.Record(HydroClient.Source, "height", hydro.GetHeights(stations, today.AddDays(-1), today.AddDays(1), ct), ct);
        if (heights is null)
        {
            return new ImportLakeLevelsResult(0, stations.Count, Failed: true);
        }

        var withValues = stations.Where(s => heights.GetValueOrDefault(s) is { Count: > 0 }).ToList();
        var readings = await dbContext.LakeLevelReadings.Where(r => withValues.Contains(r.StationId)).ToDictionaryAsync(r => r.StationId, ct);
        foreach (var station in withValues)
        {
            if (!readings.TryGetValue(station, out var reading))
            {
                reading = new LakeLevelReading { StationId = station };
                dbContext.LakeLevelReadings.Add(reading);
                readings[station] = reading;
            }

            var latest = heights[station][^1];
            reading.MeasuredAt = latest.Time;
            reading.Level = Math.Round((decimal)latest.Value, 3);
            reading.ImportedAt = now;
        }

        await dbContext.SaveChangesAsync(ct);
        var norms = await ImportNorms([.. readings.Values], recorder, ct);
        return new ImportLakeLevelsResult(withValues.Count, stations.Count - withValues.Count, Norms: norms);
    }

    /// <summary>
    /// Pro Station und Schweizer Kalendertag ein Abruf. Ohne Statistik (404, Station ohne Band) bleibt die alte bis zum nächsten Tag.
    /// Bei einem Ausfall hört der Abruf auf, und der nächste Lauf versucht es wieder.
    /// </summary>
    private async Task<int> ImportNorms(List<LakeLevelReading> readings, SourceRecorder recorder, CancellationToken ct)
    {
        var today = SwissTime.Today(time);
        var imported = 0;
        var calls = 0;
        foreach (var reading in readings.Where(r => r.NormFetchedOn != today).OrderBy(r => r.StationId, StringComparer.Ordinal))
        {
            if (calls++ > 0)
            {
                await Task.Delay(BetweenCalls, time, ct);
            }

            var result = await recorder.Record(BafuStatisticsClient.Source, $"p_annual-{reading.StationId}", statistics.GetAnnual(reading.StationId, ct), ct);
            if (recorder.LastCall is { Ok: false, StatusCode: null or >= 500 })
            {
                break;
            }

            reading.NormFetchedOn = today;
            if (result?.For(today) is { } norm)
            {
                reading.NormDay = today;
                reading.NormLow = norm.Low;
                reading.NormMedian = norm.Median;
                reading.NormHigh = norm.High;
                imported++;
            }

            await dbContext.SaveChangesAsync(ct);
        }

        return imported;
    }

    /// <summary>Die Pegelstationen aller Seen mit Plätzen, die nicht archiviert sind.</summary>
    private async Task<List<string>> StationsToImport(CancellationToken ct)
    {
        var sites = await dbContext.DiveSites
            .AsNoTracking()
            .Include(s => s.WaterBody).ThenInclude(w => w!.HydroStations)
            .Where(s => s.Status != DiveSiteStatus.Archived && s.WaterBody!.WaterBodyType != WaterBodyType.River)
            .ToListAsync(ct);
        return sites
            .Select(s => LakeLevelStations.For(s.Location, s.WaterBody!)?.StationId)
            .OfType<string>()
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();
    }
}
