using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Locks;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Forecasts.Calculation;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.Forecasts.Commands;

/// <summary>
/// Rechnet gespeicherte Tagesprognosen mit der aktuellen Regel neu und baut den Median pro Gewässer für alle vergangenen Tage neu auf
/// (`forecast.md`, «Neuberechnung»). Nur Admins. Ohne externe Calls: Die Eingaben stehen in der Zeile jedes Tages.
/// </summary>
public record RecomputeForecastHistoryCommand(string AdminId);

/// <summary>
/// <see cref="Recomputed"/>: Tage mit älterer Regel, die neu gerechnet wurden. <see cref="Changed"/>: davon mit anderem Index.
/// <see cref="Skipped"/>: Ein Prognose-Lauf hielt den Lock, nichts geändert.
/// </summary>
public record RecomputeForecastHistoryResult(AccountError? Error, bool Skipped = false, int Recomputed = 0, int Changed = 0, int Days = 0);

internal class RecomputeForecastHistoryHandler(
    TauchplaetzeDbContext dbContext,
    AccountRoles roles,
    IRunLock runLock,
    WaterBodyIndexWriter indexWriter,
    TimeProvider time) : ICommandHandler<RecomputeForecastHistoryCommand, RecomputeForecastHistoryResult>
{
    private const int DaysPerBatch = 31;

    public async Task<RecomputeForecastHistoryResult> Handle(RecomputeForecastHistoryCommand command, CancellationToken ct = default)
    {
        var rights = await roles.Of(command.AdminId);
        if (!rights.CanAdminister)
        {
            return new RecomputeForecastHistoryResult(rights.IsBlocked ? AccountError.Blocked : AccountError.NotAllowed);
        }

        await using var acquired = await runLock.TryAcquire(RunForecastHandler.LockName, ct);
        if (acquired is null)
        {
            return new RecomputeForecastHistoryResult(null, Skipped: true);
        }

        var rules = DiveIndexRules.Default;
        var today = SwissTime.Today(time);
        var rivers = await dbContext.DiveSites
            .Where(s => s.WaterBody!.WaterBodyType == WaterBodyType.River)
            .Select(s => s.Id)
            .ToHashSetAsync(ct);
        var dates = await dbContext.DailyForecasts.Select(f => f.Date).Distinct().OrderBy(d => d).ToListAsync(ct);

        var recomputed = 0;
        var changed = 0;
        foreach (var batch in dates.Chunk(DaysPerBatch))
        {
            var stale = await dbContext.DailyForecasts
                .Where(f => batch.Contains(f.Date) && f.ModelVersion != rules.ModelVersion)
                .ToListAsync(ct);
            foreach (var forecast in stale)
            {
                var before = forecast.DiveIndex;
                Recompute(forecast, rivers.Contains(forecast.DiveSiteId), rules, time.GetUtcNow());
                recomputed++;
                changed += before == forecast.DiveIndex ? 0 : 1;
            }

            await dbContext.SaveChangesAsync(ct);
            dbContext.ChangeTracker.Clear();
            await indexWriter.Write([.. batch.Where(d => d <= today)], ct);
        }

        return new RecomputeForecastHistoryResult(null, false, recomputed, changed, dates.Count(d => d <= today));
    }

    /// <remarks>Eine Zeile vor `rules-1.2` erhält dabei den Wert in Ufernähe, mit dem sie gerechnet wurde, damit Verlauf und Mini-Diagramm die Sicht zeigen.</remarks>
    private static void Recompute(DailyForecast forecast, bool isRiver, DiveIndexRules rules, DateTimeOffset now)
    {
        var inputs = InputsOf(forecast, isRiver, rules);
        var result = DiveIndexCalculator.Calculate(inputs, rules);
        forecast.SecchiDepthShore = inputs.Clarity.ShoreDepth is { } shore ? Math.Round((decimal)shore, 1) : null;
        forecast.ClaritySource = inputs.Clarity.Source;
        forecast.ComputedAt = now;
        forecast.DiveIndex = result.DiveIndex is { } index ? Math.Round((decimal)index, 1) : null;
        forecast.VisibilityGoodBelow = result.VisibilityGoodBelow is { } below ? Math.Round((decimal)below, 1) : null;
        forecast.ReasonCodes = string.Join(',', result.Reasons);
        forecast.ModelVersion = rules.ModelVersion;
    }

    /// <remarks>
    /// Zeilen vor `rules-1.2` haben keinen Wert in Ufernähe. Dann gilt der Wert der Seemitte mal Uferfaktor, wie damals im Index.
    /// Die Sprungschicht bleibt die gespeicherte: Aus vier Punkten des Profils lässt sie sich nicht neu bestimmen.
    /// </remarks>
    internal static DayInputs InputsOf(DailyForecast forecast, bool isRiver, DiveIndexRules rules)
    {
        var weather = new DayWeather(
            forecast.WeatherModel,
            (double?)forecast.WindSpeedKmh,
            (double?)forecast.WindGustsKmh,
            forecast.WindDirection,
            (double?)forecast.PrecipitationOnDay,
            (double?)forecast.PrecipitationLast72h);

        var profile = forecast.ProfileSource is { } source ? new DayProfile(ProfileOf(forecast), source) : null;
        var shore = forecast.SecchiDepthShore is { } stored
            ? (double)stored
            : forecast.SecchiDepth is { } lake ? Math.Round((double)lake * rules.ShoreFactor, 1) : (double?)null;
        var clarity = new ClarityEstimate(
            (double?)forecast.SecchiDepth,
            forecast.SecchiObservedAt,
            forecast.VisibilityIndicator,
            shore,
            shore is not null && forecast.SecchiObservedAt is { } observed ? forecast.Date.DayNumber - observed.DayNumber : null,
            forecast.ClaritySource ?? (shore is null ? null : ClaritySource.Sentinel3Lake));

        var thermocline = forecast is { ThermoclineTop: { } top, ThermoclineBottom: { } bottom } ? new Thermocline((double)top, (double)bottom) : null;
        return new DayInputs(weather, profile, clarity, forecast.AlgaeLevel, (double?)forecast.InflowDischargeRatio, isRiver, new KnownThermocline(thermocline));
    }

    private static TemperatureProfile ProfileOf(DailyForecast forecast)
    {
        (double Depth, decimal? Temperature)[] points =
            [(0, forecast.SurfaceTemperature), (10, forecast.TemperatureAt10m), (20, forecast.TemperatureAt20m), (30, forecast.TemperatureAt30m)];
        return new TemperatureProfile(
            SwissTime.ToUtc(forecast.Date, 12),
            [.. points.Where(p => p.Temperature is not null).Select(p => new ProfilePoint(p.Depth, (double)p.Temperature!.Value))]);
    }
}
