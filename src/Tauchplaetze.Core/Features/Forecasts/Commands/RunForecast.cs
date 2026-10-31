using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Locks;
using Tauchplaetze.Core.CrossCutting.Storage;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Forecasts.Calculation;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.Forecasts.Commands;

/// <summary>Ein Lauf des Prognose-Jobs: Daten holen, Index pro Platz und Tag berechnen, speichern (`forecast.md`, «Der Job»).</summary>
public record RunForecastCommand;

/// <summary>Ergebnis eines Laufs. <see cref="Skipped"/>: Ein anderer Lauf hielt den Lock.</summary>
public record RunForecastResult(bool Skipped, Guid? RunId = null, ForecastRunStatus? Status = null, int Sites = 0);

internal class RunForecastHandler(
    TauchplaetzeDbContext dbContext,
    ForecastSources sources,
    IRawDataStore rawData,
    IRunLock runLock,
    ForecastProgress progress,
    WaterBodyIndexWriter indexWriter,
    TimeProvider time,
    ILogger<RunForecastHandler> logger) : ICommandHandler<RunForecastCommand, RunForecastResult>
{
    public const string LockName = "forecast";

    /// <summary>Der Lauf speichert nach so vielen Plätzen. Ein einziges Speichern am Ende lastet SQL Basic minutenlang voll aus.</summary>
    private const int SitesPerSave = 20;

    public async Task<RunForecastResult> Handle(RunForecastCommand command, CancellationToken ct = default)
    {
        await using var acquired = await runLock.TryAcquire(LockName, ct);
        if (acquired is null)
        {
            return new RunForecastResult(Skipped: true);
        }

        var today = SwissTime.Today(time);
        var run = new ForecastRun
        {
            Id = Guid.CreateVersion7(time.GetUtcNow()),
            StartedAt = time.GetUtcNow(),
            Status = ForecastRunStatus.Running,
            RawDataPath = "",
        };
        run.RawDataPath = $"{today:yyyy-MM-dd}/{run.Id}";
        dbContext.ForecastRuns.Add(run);
        await dbContext.SaveChangesAsync(ct);

        var recorder = new SourceRecorder(rawData, run.RawDataPath);
        try
        {
            var sites = await dbContext.DiveSites
                .Include(d => d.WaterBody!).ThenInclude(w => w.HydroStations)
                .Where(d => d.Status == DiveSiteStatus.Published)
                .ToListAsync(ct);

            run.Sites = sites.Count(s => !s.ForecastDisabled);
            progress.Start(run.Id, run.Sites.Value);
            await Forecast(sites, today, run, recorder, ct);
            await dbContext.SaveChangesAsync(ct);
            await indexWriter.Write([today], ct);
            run.Status = recorder.AnyFailed ? ForecastRunStatus.PartiallySucceeded : ForecastRunStatus.Succeeded;
            return new RunForecastResult(false, run.Id, run.Status, sites.Count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            run.Status = ForecastRunStatus.Aborted;
            throw;
        }
        catch (Exception)
        {
            run.Status = ForecastRunStatus.Failed;
            throw;
        }
        finally
        {
            progress.Finish(run.Id);
            run.FinishedAt = time.GetUtcNow();
            run.SourceStatus = recorder.StatusJson;
            await dbContext.SaveChangesAsync(CancellationToken.None);
            LogFinished(run);
        }
    }

    /// <summary>Die Zeile, auf die der Alert `alert-forecast-missing` prüft. Ohne Trace-Kontext, damit kein Sampling des Laufs sie verwirft.</summary>
    private void LogFinished(ForecastRun run)
    {
        var activity = Activity.Current;
        Activity.Current = null;
        try
        {
            logger.LogInformation("Forecast run {RunId}: {Status}, {Sites} sites, skipped {Skipped}", run.Id, run.Status, run.Sites ?? 0, false);
        }
        finally
        {
            Activity.Current = activity;
        }
    }

    private async Task Forecast(IReadOnlyList<DiveSite> sites, DateOnly today, ForecastRun run, SourceRecorder recorder, CancellationToken ct)
    {
        var days = Enumerable.Range(0, ForecastSources.ForecastDays).Select(today.AddDays).ToList();
        var existing = await dbContext.DailyForecasts
            .Where(f => days.Contains(f.Date))
            .ToDictionaryAsync(f => (f.DiveSiteId, f.Date), ct);
        var outlooks = await dbContext.SiteWeatherOutlooks.ToDictionaryAsync(o => o.DiveSiteId, ct);
        var waterBodies = new Dictionary<int, WaterBodyData>();
        var inflowForecasts = new Dictionary<int, IReadOnlyDictionary<(string StationId, DateOnly Day), double>>();
        var forecastHours = await ForecastHours(ct);
        var storedMedians = await StoredMedians(ct);
        var now = time.GetUtcNow();
        var forecastSites = sites.Where(s => !s.ForecastDisabled).ToList();
        await sources.PrepareWeather(forecastSites, recorder, ct);
        var done = 0;

        foreach (var site in forecastSites)
        {
            var waterBody = site.WaterBody!;
            if (!waterBodies.TryGetValue(waterBody.Id, out var waterBodyData))
            {
                waterBodyData = Fresh(await sources.ForWaterBody(waterBody, today, recorder, ct), now);
                waterBodies[waterBody.Id] = waterBodyData;
                inflowForecasts[waterBody.Id] = InflowForecast.Ratios(WithStored(waterBodyData.Medians, waterBody.HydroStations.Select(s => s.StationId), storedMedians), forecastHours);
            }

            var siteData = await sources.ForSite(site, waterBody, today, recorder, ct);
            var inflows = SiteInflows.For(site, waterBody);
            var (measured, forecastRatios) = await StationsOf(inflows, waterBodyData, inflowForecasts[waterBody.Id], forecastHours, storedMedians, today, now, recorder, ct);
            SaveOutlook(outlooks.GetValueOrDefault(site.Id) ?? AddOutlook(site.Id), today, siteData, run);
            foreach (var day in days)
            {
                var forecast = existing.GetValueOrDefault((site.Id, day)) ?? Add(site.Id, day);
                var dischargeRatio = SiteInflows.Ratio(inflows, day, today, measured, forecastRatios);
                var dayForecast = DayFor(day, today, waterBody, waterBodyData, dischargeRatio, siteData);
                Fill(forecast, dayForecast, run);
                if (day == today)
                {
                    site.DataLevel = dayForecast.DataLevel;
                }
            }

            progress.SiteDone(run.Id, recorder.Count);
            if (++done % SitesPerSave == 0)
            {
                await dbContext.SaveChangesAsync(ct);
            }
        }
    }

    /// <summary>
    /// Die Quotienten der Stationen des Platzes. Eine eingetragene Station, die nicht am Gewässer hängt, holt der Lauf dazu.
    /// So rechnet ein Flussplatz mit derselben Station wie seine Kachel «Abfluss».
    /// </summary>
    private async Task<(IReadOnlyDictionary<string, double> Measured, IReadOnlyDictionary<(string StationId, DateOnly Day), double> Forecast)> StationsOf(
        IReadOnlyList<SiteInflow> inflows,
        WaterBodyData waterBodyData,
        IReadOnlyDictionary<(string StationId, DateOnly Day), double> forecast,
        List<ForecastHour> forecastHours,
        IReadOnlyDictionary<string, double> storedMedians,
        DateOnly today,
        DateTimeOffset now,
        SourceRecorder recorder,
        CancellationToken ct)
    {
        var missing = inflows.Select(i => i.StationId)
            .Where(id => !waterBodyData.Ratios.ContainsKey(id) && !waterBodyData.Medians.ContainsKey(id))
            .Distinct()
            .ToList();
        if (missing.Count == 0)
        {
            return (waterBodyData.Ratios, forecast);
        }

        var measured = new Dictionary<string, double>(waterBodyData.Ratios);
        var medians = new Dictionary<string, double>();
        foreach (var stationId in missing)
        {
            if (await sources.StationFlow(stationId, today, recorder, ct) is { } flow)
            {
                if (IsFresh(flow.MeasuredAt, now))
                {
                    measured[stationId] = flow.Ratio;
                }

                medians[stationId] = flow.Median;
            }
            else if (storedMedians.TryGetValue(stationId, out var stored))
            {
                medians[stationId] = stored;
            }
        }

        var forecastRatios = new Dictionary<(string StationId, DateOnly Day), double>(forecast);
        foreach (var (key, value) in InflowForecast.Ratios(medians, forecastHours))
        {
            forecastRatios[key] = value;
        }

        return (measured, forecastRatios);
    }

    /// <summary>Nur frische Messwerte zählen als «gemessen» (`rules-1.7`). Ein älterer Wert lässt am Fluss die Vorhersage für heute zum Zug kommen.</summary>
    private static WaterBodyData Fresh(WaterBodyData data, DateTimeOffset now) =>
        data with
        {
            Ratios = data.Ratios
                .Where(r => data.MeasuredAt?.TryGetValue(r.Key, out var at) != true || IsFresh(at, now))
                .ToDictionary(r => r.Key, r => r.Value),
        };

    private static bool IsFresh(DateTimeOffset measuredAt, DateTimeOffset now) =>
        now - measuredAt <= TimeSpan.FromHours(DiveIndexRules.Default.MaxDischargeAgeHours);

    /// <summary>Fällt der Abruf der Messwerte aus, gilt für die Vorhersage der zuletzt gespeicherte Median der Station.</summary>
    private static IReadOnlyDictionary<string, double> WithStored(
        IReadOnlyDictionary<string, double> medians, IEnumerable<string> stationIds, IReadOnlyDictionary<string, double> stored)
    {
        var merged = new Dictionary<string, double>(medians);
        foreach (var stationId in stationIds)
        {
            if (!merged.ContainsKey(stationId) && stored.TryGetValue(stationId, out var median))
            {
                merged[stationId] = median;
            }
        }

        return merged;
    }

    private async Task<IReadOnlyDictionary<string, double>> StoredMedians(CancellationToken ct) =>
        (await dbContext.HydroReadings
            .AsNoTracking()
            .Where(r => r.Median30Days != null)
            .Select(r => new { r.StationId, r.Median30Days })
            .ToListAsync(ct))
        .ToDictionary(r => r.StationId, r => Math.Max((double)r.Median30Days!.Value, (double)DischargeRules.MinMedian));

    private SiteWeatherOutlook AddOutlook(Guid diveSiteId)
    {
        var outlook = new SiteWeatherOutlook { DiveSiteId = diveSiteId, Hours = "[]" };
        dbContext.SiteWeatherOutlooks.Add(outlook);
        return outlook;
    }

    private void SaveOutlook(SiteWeatherOutlook outlook, DateOnly today, SiteData siteData, ForecastRun run)
    {
        outlook.FromDate = today;
        outlook.Hours = JsonSerializer.Serialize(WeatherOutlook.For(today, ForecastSources.ForecastDays, siteData.Weather), WeatherOutlookJson.Options);
        outlook.ForecastRunId = run.Id;
        outlook.ComputedAt = time.GetUtcNow();
    }

    private DailyForecast Add(Guid diveSiteId, DateOnly day)
    {
        var forecast = new DailyForecast { DiveSiteId = diveSiteId, Date = day, ModelVersion = DiveIndexRules.Default.ModelVersion };
        dbContext.DailyForecasts.Add(forecast);
        return forecast;
    }

    private static DayForecast DayFor(
        DateOnly day, DateOnly today, WaterBody waterBody, WaterBodyData waterBodyData, InflowRatio discharge, SiteData siteData)
    {
        var rules = DiveIndexRules.Default;
        var isRiver = waterBody.WaterBodyType == WaterBodyType.River;
        var weather = DayWeather.For(day, siteData.Weather, DiveWindow.Default);
        var profile = isRiver ? RiverProfile(day, today, waterBodyData) : DataLevelRule.ChooseProfile(day, siteData.Model3d, waterBodyData.LakeWide);

        var clarity = ClarityEstimate.Choose(
            day,
            siteData.ShoreSecchi,
            siteData.Secchi,
            imageDate => DayWeather.RainFromBestModel(siteData.Weather, imageDate.ToDateTime(TimeOnly.MinValue), day.ToDateTime(TimeOnly.MinValue)) ?? weather.PrecipitationLast72h,
            discharge.Value,
            rules);
        var algae = ClarityEstimate.AlgaeFor(day, siteData.Chlorophyll, rules);

        var inputs = new DayInputs(weather, profile, clarity, algae, discharge.Value, isRiver, DischargeIsCurrent: discharge.IsCurrent);
        var current = isRiver ? null : CurrentEstimate.For(day, siteData.Model3d, DiveWindow.Default, CurrentRules.Default);
        var source = profile?.Source ?? (isRiver && discharge is { Value: not null, IsCurrent: true } ? ProfileSource.RiverStation : null);
        return new DayForecast(inputs, DiveIndexCalculator.Calculate(inputs, rules), DataLevelRule.For(false, source), current);
    }

    private async Task<List<ForecastHour>> ForecastHours(CancellationToken ct) =>
        (await dbContext.HydroHours
            .AsNoTracking()
            .Where(h => h.Kind == HydroSeriesKind.Forecast)
            .Select(h => new { h.StationId, h.Hour, h.Discharge })
            .ToListAsync(ct))
        .Select(h => new ForecastHour(h.StationId, h.Hour, (double)h.Discharge))
        .ToList();

    /// <remarks>Die Station misst nur heute. Für die Tage danach gibt es keine Wassertemperatur am Fluss.</remarks>
    private static DayProfile? RiverProfile(DateOnly day, DateOnly today, WaterBodyData data) =>
        data.RiverTemperature is { } temperature && day == today
            ? new DayProfile(new TemperatureProfile(SwissTime.ToUtc(today, 12), [new ProfilePoint(0, temperature)]), ProfileSource.RiverStation)
            : null;

    private void Fill(DailyForecast forecast, DayForecast day, ForecastRun run)
    {
        var (inputs, result, dataLevel, current) = day;
        var profile = inputs.Profile?.Profile;

        forecast.DataLevel = dataLevel;
        forecast.DiveIndex = ToDecimal(result.DiveIndex);
        forecast.ProfileSource = inputs.Profile?.Source;
        forecast.SurfaceTemperature = ToDecimal(profile?.SurfaceTemperature);
        forecast.TemperatureAt10m = ToDecimal(profile?.TemperatureAt(10));
        forecast.TemperatureAt20m = ToDecimal(profile?.TemperatureAt(20));
        forecast.TemperatureAt30m = ToDecimal(profile?.TemperatureAt(30));
        forecast.ThermoclineTop = ToDecimal(result.StoredThermocline?.Top);
        forecast.ThermoclineBottom = ToDecimal(result.StoredThermocline?.Bottom);
        forecast.SecchiDepth = ToDecimal(inputs.Clarity.SecchiDepth);
        forecast.SecchiDepthShore = ToDecimal(inputs.Clarity.ShoreDepth);
        forecast.SecchiObservedAt = inputs.Clarity.ObservedAt;
        forecast.ClaritySource = inputs.Clarity.Source;
        forecast.VisibilityIndicator = inputs.Clarity.Indicator;
        forecast.VisibilityGoodBelow = ToDecimal(result.VisibilityGoodBelow);
        forecast.WindBeaufort = inputs.Weather.WindBeaufort;
        forecast.WindSpeedKmh = ToDecimal(inputs.Weather.WindMaxKmh);
        forecast.WindGustsKmh = ToDecimal(inputs.Weather.GustMaxKmh);
        forecast.WindDirection = inputs.Weather.WindDirection is { } direction ? (int)Math.Round(direction) : null;
        forecast.WeatherModel = inputs.Weather.Model;
        forecast.CurrentSpeed = current is null ? null : Math.Round((decimal)current.Speed, 3);
        forecast.CurrentDirection = current is null ? null : (int)Math.Round(current.DirectionToward) % 360;
        forecast.CurrentLevel = current?.Level;
        forecast.PrecipitationLast72h = ToDecimal(inputs.Weather.PrecipitationLast72h);
        forecast.PrecipitationOnDay = ToDecimal(inputs.Weather.PrecipitationInDiveWindow);
        forecast.InflowDischargeRatio = inputs.DischargeRatio is { } ratio ? Math.Round((decimal)ratio, 2) : null;
        forecast.AlgaeLevel = inputs.Algae;
        forecast.ReasonCodes = string.Join(',', result.Reasons);
        forecast.ModelVersion = DiveIndexRules.Default.ModelVersion;
        forecast.ForecastRunId = run.Id;
        forecast.ComputedAt = time.GetUtcNow();
    }

    private static decimal? ToDecimal(double? value) => value is { } v ? Math.Round((decimal)v, 1) : null;

    private sealed record DayForecast(DayInputs Inputs, DayResult Result, byte DataLevel, DayCurrent? Current);
}
