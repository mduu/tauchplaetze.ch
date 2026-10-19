using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Locks;
using Tauchplaetze.Core.CrossCutting.Storage;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Forecasts.Calculation;
using Tauchplaetze.Core.Features.Forecasts.Sources.Hydro;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.Forecasts.Commands;

/// <summary>
/// Holt Abfluss und Wassertemperatur der Stationen aller Flussplätze in einem Call und speichert Stundenmittel und die neuesten Messwerte
/// (`forecast.md`, «Abfluss»). Stundenwerte älter als 30 Tage fallen weg. Ein zweiter Lauf schreibt dieselben Werte.
/// </summary>
public record ImportDischargeCommand;

/// <summary>Ergebnis: Stationen mit Werten und Stationen ohne Werte. <see cref="Failed"/>: Der Call scheiterte. <see cref="Skipped"/>: Ein anderer Lauf hielt den Lock.</summary>
public record ImportDischargeResult(int Stations, int StationsWithoutValues, bool Failed = false, bool Skipped = false);

internal class ImportDischargeHandler(TauchplaetzeDbContext dbContext, HydroClient hydro, IRawDataStore rawData, IRunLock runLock, TimeProvider time)
    : ICommandHandler<ImportDischargeCommand, ImportDischargeResult>
{
    public const string LockName = "discharge";

    /// <summary>So lange bleiben die Stundenwerte. Der Median der Einordnung braucht sie.</summary>
    public static readonly TimeSpan KeepFor = TimeSpan.FromDays(30);

    private static readonly TimeSpan TrendSpan = TimeSpan.FromHours(3);

    public async Task<ImportDischargeResult> Handle(ImportDischargeCommand command, CancellationToken ct = default)
    {
        await using var acquired = await runLock.TryAcquire(LockName, ct);
        if (acquired is null)
        {
            return new ImportDischargeResult(0, 0, Skipped: true);
        }

        var stations = await StationsOfRiverSites(ct);
        if (stations.Count == 0)
        {
            return new ImportDischargeResult(0, 0);
        }

        var now = time.GetUtcNow();
        var from = await FetchFrom(stations, now, ct);
        var recorder = new SourceRecorder(rawData, $"discharge/{now:yyyy-MM-dd}/{now:HHmm}");
        var series = await recorder.Record(HydroClient.Source, "flow-temperature",
            hydro.GetFlowsAndTemperatures(stations, DateOnly.FromDateTime(from.UtcDateTime), DateOnly.FromDateTime(now.UtcDateTime).AddDays(1), ct), ct);
        if (series is null)
        {
            return new ImportDischargeResult(0, stations.Count, Failed: true);
        }

        var values = series.Flow;

        var withValues = stations.Where(s => values.GetValueOrDefault(s) is { Count: > 0 }).ToList();
        foreach (var station in withValues)
        {
            await SaveHours(station, values[station], now, ct);
        }

        await dbContext.SaveChangesAsync(ct);
        var cutoff = now - KeepFor;
        await dbContext.HydroHours.Where(h => h.Hour < cutoff).ExecuteDeleteAsync(ct);

        foreach (var station in withValues)
        {
            await SaveReading(station, values[station], series.Temperature.GetValueOrDefault(station), now, ct);
        }

        await dbContext.SaveChangesAsync(ct);
        return new ImportDischargeResult(withValues.Count, stations.Count - withValues.Count);
    }

    private async Task<List<string>> StationsOfRiverSites(CancellationToken ct)
    {
        var sites = await dbContext.DiveSites
            .AsNoTracking()
            .Include(s => s.WaterBody).ThenInclude(w => w!.HydroStations)
            .Where(s => s.Status != DiveSiteStatus.Archived && s.WaterBody!.WaterBodyType == WaterBodyType.River)
            .ToListAsync(ct);
        return sites
            .Select(s => DischargeStations.For(s.Location, s.InflowStationOverride, s.WaterBody!))
            .OfType<string>()
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Ab der letzten gespeicherten Stunde, mindestens die Spanne des Trends. Eine neue Station holt 30 Tage nach.
    /// existenz.ch liest `startdate` als UTC-Tag (Antwort vom 19.10.2026 beginnt um 00:00 UTC).
    /// </summary>
    private async Task<DateTimeOffset> FetchFrom(List<string> stations, DateTimeOffset now, CancellationToken ct)
    {
        var latest = await dbContext.HydroHours
            .Where(h => stations.Contains(h.StationId) && h.Kind == HydroSeriesKind.Measured)
            .GroupBy(h => h.StationId)
            .Select(g => new { StationId = g.Key, Hour = g.Max(h => h.Hour) })
            .ToDictionaryAsync(x => x.StationId, x => x.Hour, ct);
        var oldest = stations.Min(s => latest.TryGetValue(s, out var hour) ? hour : now - KeepFor);
        var trendStart = now - TrendSpan - TimeSpan.FromHours(1);
        return oldest < trendStart ? oldest : trendStart;
    }

    private async Task SaveHours(string station, IReadOnlyList<HydroValue> values, DateTimeOffset now, CancellationToken ct)
    {
        var cutoff = now - KeepFor;
        var hours = DischargeRules.HourlyMeans(values).Where(h => h.Hour >= cutoff).ToList();
        if (hours.Count == 0)
        {
            return;
        }

        var first = hours[0].Hour;
        var existing = await dbContext.HydroHours
            .Where(h => h.StationId == station && h.Kind == HydroSeriesKind.Measured && h.Hour >= first)
            .ToDictionaryAsync(h => h.Hour, ct);
        foreach (var (hour, discharge) in hours)
        {
            if (!existing.TryGetValue(hour, out var row))
            {
                row = new HydroHour { StationId = station, Kind = HydroSeriesKind.Measured, Hour = hour };
                dbContext.HydroHours.Add(row);
            }

            row.Discharge = discharge;
            row.ImportedAt = now;
        }
    }

    /// <remarks>Ohne Temperatur in diesem Call bleibt die letzte. Die Kachel zeigt sie mit Uhrzeit und nach 2 h als veraltet.</remarks>
    private async Task SaveReading(string station, IReadOnlyList<HydroValue> values, IReadOnlyList<HydroValue>? temperatures, DateTimeOffset now, CancellationToken ct)
    {
        var latest = values[^1];
        var cutoff = now - KeepFor;
        var hours = await dbContext.HydroHours
            .AsNoTracking()
            .Where(h => h.StationId == station && h.Kind == HydroSeriesKind.Measured && h.Hour >= cutoff)
            .Select(h => h.Discharge)
            .ToListAsync(ct);
        var reading = await dbContext.HydroReadings.FindAsync([station], ct);
        if (reading is null)
        {
            reading = new HydroReading { StationId = station };
            dbContext.HydroReadings.Add(reading);
        }

        reading.MeasuredAt = latest.Time;
        reading.Discharge = Math.Round((decimal)latest.Value, 2);
        reading.DischargeThreeHoursBefore = DischargeRules.Nearest(values, latest.Time - TrendSpan) is { } before ? Math.Round((decimal)before.Value, 2) : null;
        reading.Median30Days = DischargeRules.Median(hours);
        if (temperatures is [.., var temperature])
        {
            reading.Temperature = Math.Round((decimal)temperature.Value, 1);
            reading.TemperatureMeasuredAt = temperature.Time;
        }

        reading.ImportedAt = now;
    }
}
