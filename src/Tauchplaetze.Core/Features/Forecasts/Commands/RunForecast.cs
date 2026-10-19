using System.Text.Json;
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
    TimeProvider time) : ICommandHandler<RunForecastCommand, RunForecastResult>
{
    public const string LockName = "forecast";

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

            await Forecast(sites, today, run, recorder, ct);
            run.Status = recorder.AnyFailed ? ForecastRunStatus.PartiallySucceeded : ForecastRunStatus.Succeeded;
            return new RunForecastResult(false, run.Id, run.Status, sites.Count);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            run.Status = ForecastRunStatus.Failed;
            throw;
        }
        finally
        {
            run.FinishedAt = time.GetUtcNow();
            run.SourceStatus = recorder.StatusJson;
            await dbContext.SaveChangesAsync(CancellationToken.None);
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

        foreach (var site in sites.Where(s => !s.ForecastDisabled))
        {
            var waterBody = site.WaterBody!;
            if (!waterBodies.TryGetValue(waterBody.Id, out var waterBodyData))
            {
                waterBodyData = await sources.ForWaterBody(waterBody, today, recorder, ct);
                waterBodies[waterBody.Id] = waterBodyData;
            }

            var siteData = await sources.ForSite(site, waterBody, today, recorder, ct);
            SaveOutlook(outlooks.GetValueOrDefault(site.Id) ?? AddOutlook(site.Id), today, siteData, run);
            foreach (var day in days)
            {
                var forecast = existing.GetValueOrDefault((site.Id, day)) ?? Add(site.Id, day);
                var dayForecast = DayFor(day, today, waterBody, waterBodyData, siteData);
                Fill(forecast, dayForecast, run);
                if (day == today)
                {
                    site.DataLevel = dayForecast.DataLevel;
                }
            }
        }
    }

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

    private static DayForecast DayFor(DateOnly day, DateOnly today, WaterBody waterBody, WaterBodyData waterBodyData, SiteData siteData)
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
            waterBodyData.DischargeRatio,
            rules);
        var algae = ClarityEstimate.AlgaeFor(day, siteData.Chlorophyll, rules);

        var inputs = new DayInputs(weather, profile, clarity, algae, waterBodyData.DischargeRatio, isRiver);
        var current = isRiver ? null : CurrentEstimate.For(day, siteData.Model3d, DiveWindow.Default, CurrentRules.Default);
        return new DayForecast(inputs, DiveIndexCalculator.Calculate(inputs, rules), DataLevelRule.For(false, profile?.Source), current);
    }

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
        forecast.ThermoclineTop = ToDecimal(result.Thermocline?.Top);
        forecast.ThermoclineBottom = ToDecimal(result.Thermocline?.Bottom);
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
