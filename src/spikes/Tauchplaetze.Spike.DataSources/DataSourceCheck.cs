using Microsoft.Extensions.Options;
using Tauchplaetze.Spike.DataSources.Alplakes;
using Tauchplaetze.Spike.DataSources.Analysis;
using Tauchplaetze.Spike.DataSources.Hydro;
using Tauchplaetze.Spike.DataSources.Weather;

namespace Tauchplaetze.Spike.DataSources;

/// <summary>
/// Queries all sources for one dive site and derives the values that the forecast needs.
/// </summary>
internal class DataSourceCheck(
    AlplakesClient alplakes,
    OpenMeteoClient weather,
    HydroClient hydro,
    RawResponseStore store,
    IOptions<SpikeOptions> options,
    TimeProvider time,
    ILogger<DataSourceCheck> logger)
{
    private const string SecchiSatellite = "sentinel3";
    private const string SecchiVariable = "Zsd_lee";
    private const string ChlorophyllVariable = "chla";
    private const int DiveWindowStartHour = 8;
    private const int DiveWindowEndHour = 16;
    private const int PrecipitationDays = 3;
    private const int MinDiveWindowHours = 7;

    private static readonly TimeZoneInfo SwissTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    private readonly SpikeOptions settings = options.Value;

    public async Task<SpikeReport> Run(SpikeSite site, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, SwissTimeZone).DateTime);
        var dayCount = Math.Max(settings.ForecastDays, settings.WeatherModels.Select(m => m.ForecastDays).DefaultIfEmpty(0).Max());
        var days = Enumerable.Range(0, dayCount).Select(today.AddDays).ToList();

        var (coverage, coverageCategory, simulationLakes) = await CheckCoverage(site, ct);
        var (temperatureCategory, profilesByDay, model) = await CheckTemperature(site, simulationLakes, today, ct);
        var oneDimensionalCategory = await CheckOneDimensionalModel(site, now, ct);
        var (satelliteCategories, satellite) = await CheckSatellite(site, today, coverage, ct);
        var (weatherCategories, weatherModels) = await CheckWeather(site, ct);
        var (hydroCategory, hydroSummaries) = await CheckHydro(site, today, now, ct);

        var dayConditions = days
            .Select(day => BuildDay(day, model, profilesByDay.GetValueOrDefault(day), weatherModels))
            .ToList();

        var report = new SpikeReport(
            site.Name,
            site.Latitude,
            site.Longitude,
            now,
            store.RunDirectory,
            [coverageCategory, temperatureCategory, oneDimensionalCategory, .. satelliteCategories, .. weatherCategories, hydroCategory],
            dayConditions,
            satellite,
            hydroSummaries,
            coverage);

        LogSummary(report);
        return report;
    }

    private async Task<(CoverageSummary, CategoryResult, IReadOnlyList<SimulationLake>)> CheckCoverage(SpikeSite site, CancellationToken ct)
    {
        var (simulationResponse, simulationLakes) = await alplakes.GetSimulationMetadata(ct);
        var (oneDimensionalResponse, oneDimensionalLakes) = await alplakes.GetOneDimensionalLakes(ct);
        var (remoteSensingResponse, variables) = await alplakes.GetRemoteSensingProducts(site.AlplakesLake, ct);

        var coverage = new CoverageSummary(
            simulationLakes.GroupBy(l => l.Model).ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(l => l.Lake).ToList()),
            oneDimensionalLakes,
            variables,
            simulationLakes.Where(l => l.Lake == site.AlplakesLake)
                .Select(l => $"{l.Model}/{l.Lake}: {l.StartDate:yyyy-MM-dd} to {l.EndDate:yyyy-MM-dd}, missing dates: {l.MissingDates}")
                .ToList());

        var calls = new[] { simulationResponse, oneDimensionalResponse, remoteSensingResponse }.Select(CallInfo.From).ToList();
        var status = calls.All(c => c.Error is null) ? CategoryStatus.Ok : CategoryStatus.Failed;
        var summary = $"3D: {coverage.LakesBy3dModel.Sum(m => m.Value.Count)} lake models, "
                      + $"1D: {oneDimensionalLakes.Count} lakes, remote sensing for '{site.AlplakesLake}': "
                      + string.Join("; ", variables.Select(v => $"{v.Key} [{string.Join(", ", v.Value)}]"));

        return (coverage, new CategoryResult("Coverage (metadata)", status, summary, null, calls), simulationLakes);
    }

    private async Task<(CategoryResult, Dictionary<DateOnly, TemperatureProfile>, string?)> CheckTemperature(
        SpikeSite site, IReadOnlyList<SimulationLake> simulationLakes, DateOnly today, CancellationToken ct)
    {
        var start = ToUtc(today, 0);
        var end = ToUtc(today.AddDays(settings.ForecastDays), 0);
        var calls = new List<CallInfo>();
        var notes = new List<string>();
        Dictionary<DateOnly, TemperatureProfile>? primaryProfiles = null;
        string? primaryModel = null;
        DateTimeOffset? latest = null;

        foreach (var model in site.Alplakes3dModels)
        {
            var lake = simulationLakes.FirstOrDefault(l => l.Model == model && l.Lake == site.AlplakesLake);
            if (lake is null)
            {
                notes.Add($"{model}: no model for '{site.AlplakesLake}'");
                continue;
            }

            var modelEnd = lake.EndDate is { } endDate && ToUtc(endDate, 0) < end ? ToUtc(endDate, 0) : end;
            var (response, series) = await alplakes.GetDepthTime(model, site.AlplakesLake, start, modelEnd, site.Latitude, site.Longitude, ct);
            calls.Add(CallInfo.From(response));
            if (series is null || series.Times.Count == 0)
            {
                notes.Add($"{model}: no data ({response.Error})");
                continue;
            }

            var profiles = ProfilesAtNoon(series);
            var lastTime = series.Times[^1];
            notes.Add($"{model}: {series.Times.Count} steps until {lastTime:yyyy-MM-dd HH:mm}Z, {series.Depths.Count} depths "
                      + $"(max {series.Depths.Max():0.#} m), grid point {series.DistanceToGridPoint:0} m away");

            if (primaryProfiles is null)
            {
                primaryProfiles = profiles;
                primaryModel = model;
                latest = lastTime;
            }
        }

        var status = primaryProfiles switch
        {
            null when calls.All(c => c.Error is not null) && calls.Count > 0 => CategoryStatus.Failed,
            null => CategoryStatus.Missing,
            { Count: var count } when count >= Math.Min(3, settings.ForecastDays) => CategoryStatus.Ok,
            _ => CategoryStatus.Partial
        };

        return (new CategoryResult("Temperature profile (3D model)", status, string.Join(" | ", notes), latest, calls),
            primaryProfiles ?? [], primaryModel);
    }

    private async Task<CategoryResult> CheckOneDimensionalModel(SpikeSite site, DateTimeOffset now, CancellationToken ct)
    {
        if (site.Alplakes1dLake is null)
        {
            return new CategoryResult("Temperature profile (1D model, lake-wide)", CategoryStatus.Missing, "No 1D lake configured", null, []);
        }

        var response = await alplakes.GetOneDimensionalDepthTime(site.Alplakes1dLake, now.AddDays(-2), now, ct);
        var status = response.IsSuccess ? CategoryStatus.Ok : CategoryStatus.Failed;
        var summary = response.IsSuccess
            ? $"Simstrat '{site.Alplakes1dLake}' answered ({response.Body!.Length / 1024} KB). Includes the variable 'Thermocline'. Fallback only."
            : $"Simstrat '{site.Alplakes1dLake}' failed: {response.Error}";
        return new CategoryResult("Temperature profile (1D model, lake-wide)", status, summary, null, [CallInfo.From(response)]);
    }

    private async Task<(IReadOnlyList<CategoryResult>, SatelliteSummary)> CheckSatellite(
        SpikeSite site, DateOnly today, CoverageSummary coverage, CancellationToken ct)
    {
        var since = today.AddDays(-settings.SatelliteLookbackDays);
        var latitude = site.SatelliteLatitude ?? site.Latitude;
        var longitude = site.SatelliteLongitude ?? site.Longitude;
        var hasProducts = coverage.RemoteSensingVariablesBySatellite.TryGetValue(SecchiSatellite, out var variables);

        var (imagesResponse, images) = await alplakes.GetRemoteSensingImages(site.AlplakesLake, SecchiSatellite, SecchiVariable, since, ct);
        var (secchiResponse, secchi) = await alplakes.GetRemoteSensingTimeseries(
            site.AlplakesLake, SecchiSatellite, SecchiVariable, latitude, longitude, since, today, 1, ct);
        var (secchiAtSiteResponse, secchiAtSite) = await alplakes.GetRemoteSensingTimeseries(
            site.AlplakesLake, SecchiSatellite, SecchiVariable, site.Latitude, site.Longitude, since, today, 1, ct);
        var (chlorophyllResponse, chlorophyll) = await alplakes.GetRemoteSensingTimeseries(
            site.AlplakesLake, SecchiSatellite, ChlorophyllVariable, latitude, longitude, since, today, 1, ct);

        var latestSecchi = secchi.FirstOrDefault();
        var latestChlorophyll = chlorophyll.FirstOrDefault();
        var note = $"{images.Count} {SecchiSatellite} images in {settings.SatelliteLookbackDays} days "
                   + $"(valid pixels median {Median(images.Select(i => i.ValidPixels ?? 0).ToList()):0}%). "
                   + $"Values at the offshore point: {secchi.Count}. Values at the site point (shore): {secchiAtSite.Count}.";

        var satellite = new SatelliteSummary(
            latestSecchi?.Median, latestSecchi?.Time, latestChlorophyll?.Median, latestChlorophyll?.Time,
            images.Count, secchiAtSite.Count, note);

        var visibility = new CategoryResult(
            "Visibility (satellite Secchi depth)",
            SatelliteStatus(hasProducts && variables!.Contains(SecchiVariable), secchiResponse, latestSecchi, today),
            latestSecchi is null
                ? $"No Secchi value at the point. {note}"
                : $"Secchi {latestSecchi.Median:0.0} m on {latestSecchi.Time:yyyy-MM-dd} ({latestSecchi.PixelCount} pixels). {note}",
            latestSecchi?.Time,
            [CallInfo.From(imagesResponse), CallInfo.From(secchiResponse), CallInfo.From(secchiAtSiteResponse)]);

        var algae = new CategoryResult(
            "Algae (satellite chlorophyll-a)",
            SatelliteStatus(hasProducts && variables!.Contains(ChlorophyllVariable), chlorophyllResponse, latestChlorophyll, today),
            latestChlorophyll is null
                ? "No chlorophyll value at the point."
                : $"Chlorophyll-a {latestChlorophyll.Median:0.0} mg/m³ on {latestChlorophyll.Time:yyyy-MM-dd}, {chlorophyll.Count} values in {settings.SatelliteLookbackDays} days.",
            latestChlorophyll?.Time,
            [CallInfo.From(chlorophyllResponse)]);

        return ([visibility, algae], satellite);
    }

    private async Task<(IReadOnlyList<CategoryResult>, IReadOnlyList<ModelWeather>)> CheckWeather(SpikeSite site, CancellationToken ct)
    {
        var categories = new List<CategoryResult>();
        var models = new List<ModelWeather>();
        foreach (var model in settings.WeatherModels)
        {
            var category = $"Weather ({model.Label})";
            var (response, result) = await weather.GetHourly(model.Model, site.Latitude, site.Longitude, PrecipitationDays, model.ForecastDays, ct);
            if (result is null)
            {
                categories.Add(new CategoryResult(category, CategoryStatus.Failed, response.Error ?? "No data", null, [CallInfo.From(response)]));
                continue;
            }

            var withWind = result.Hours.Where(h => h.WindSpeedKmh is not null).ToList();
            var lastHour = withWind.LastOrDefault()?.LocalTime;
            var nullHours = result.Hours.Count - withWind.Count;
            var status = withWind.Count == 0 ? CategoryStatus.Missing : nullHours == 0 ? CategoryStatus.Ok : CategoryStatus.Partial;
            var summary = $"{model.Model}: {result.Hours.Count} hours, wind until {lastHour:yyyy-MM-dd HH:mm} local, "
                          + $"{nullHours} hours without wind, grid point {result.GridLatitude:0.000}/{result.GridLongitude:0.000}, elevation {result.Elevation:0} m";
            DateTimeOffset? latest = lastHour is { } local ? new DateTimeOffset(local, SwissTimeZone.GetUtcOffset(local)) : null;

            categories.Add(new CategoryResult(category, status, summary, latest, [CallInfo.From(response)]));
            models.Add(new ModelWeather(model.Model, result.Hours));
        }

        return (categories, models);
    }

    private async Task<(CategoryResult, IReadOnlyList<HydroSummary>)> CheckHydro(SpikeSite site, DateOnly today, DateTimeOffset now, CancellationToken ct)
    {
        var calls = new List<CallInfo>();
        var summaries = new List<HydroSummary>();
        foreach (var station in site.HydroStations)
        {
            var (response, values) = await hydro.GetRange(station.Id, station.Parameter, today.AddDays(-settings.HydroLookbackDays), today.AddDays(1), ct);
            calls.Add(CallInfo.From(response));

            var latest = values.LastOrDefault();
            var median = values.Count == 0 ? (double?)null : Median(values.Select(v => v.Value).ToList());
            var ratio = latest is not null && median is > 0 && station.Parameter == "flow" ? latest.Value / median : null;
            summaries.Add(new HydroSummary(station.Id, station.Name, station.Parameter, latest?.Value, latest?.Time, median, ratio));
        }

        var fresh = summaries.Count(s => s.LatestAt is { } at && now - at < TimeSpan.FromHours(3));
        var status = fresh == summaries.Count && summaries.Count > 0
            ? CategoryStatus.Ok
            : fresh > 0 ? CategoryStatus.Partial : calls.All(c => c.Error is not null) ? CategoryStatus.Failed : CategoryStatus.Missing;
        var summary = string.Join(" | ", summaries.Select(s =>
            $"{s.StationId} {s.Parameter}: {s.Latest:0.00} at {s.LatestAt:yyyy-MM-dd HH:mm}Z, median {s.Median:0.00}, ratio {s.RatioToMedian:0.00}"));

        return (new CategoryResult("Hydrology (BAFU)", status, summary, summaries.Max(s => s.LatestAt), calls), summaries);
    }

    private DayConditions BuildDay(DateOnly day, string? model, TemperatureProfile? profile, IReadOnlyList<ModelWeather> weatherModels)
    {
        var thermocline = profile is null ? null : ThermoclineDetector.Detect(profile, settings.ThermoclineGradientThreshold);

        var dayStart = day.ToDateTime(TimeOnly.MinValue);
        var windowStart = dayStart.AddHours(DiveWindowStartHour);
        var windowEnd = dayStart.AddHours(DiveWindowEndHour);
        var windByModel = weatherModels.ToDictionary(
            m => m.Model,
            m => DiveWindow(m.Hours, windowStart, windowEnd) is { Count: >= MinDiveWindowHours } window
                ? Beaufort.FromKmh(window.Max(h => h.WindSpeedKmh!.Value))
                : (int?)null);

        // The first model in order of preference that covers the whole dive window gives the wind and the rain of the day.
        var weatherModel = weatherModels.FirstOrDefault(m => windByModel[m.Model] is not null);
        var diveWindow = weatherModel is null ? [] : DiveWindow(weatherModel.Hours, windowStart, windowEnd);
        var windiest = diveWindow.MaxBy(h => h.WindSpeedKmh);
        var gusts = diveWindow.Max(h => h.WindGustsKmh);
        var dayRain = RainFromBestModel(weatherModels, dayStart, dayStart.AddDays(1));
        var lastDaysRain = RainFromBestModel(weatherModels, dayStart.AddDays(-PrecipitationDays), dayStart);

        return new DayConditions(
            day,
            profile is null ? null : model,
            Round(profile?.Points.FirstOrDefault()?.Temperature),
            Round(profile?.TemperatureAt(10)),
            Round(profile?.TemperatureAt(20)),
            Round(profile?.TemperatureAt(30)),
            Round(thermocline?.Top),
            Round(thermocline?.Bottom),
            weatherModel?.Model,
            windiest?.WindSpeedKmh is { } speed ? Beaufort.FromKmh(speed) : null,
            windiest?.WindSpeedKmh,
            gusts is { } gust ? Beaufort.FromKmh(gust) : null,
            gusts,
            windiest?.WindDirection,
            dayRain,
            lastDaysRain,
            windByModel);
    }

    /// <summary>
    /// Sums the rain of a period. Each hour takes the value of the first model in order of preference that has one.
    /// Returns <c>null</c> when an hour has no value in any model.
    /// </summary>
    internal static double? RainFromBestModel(IReadOnlyList<ModelWeather> weatherModels, DateTime start, DateTime end)
    {
        var total = 0.0;
        for (var hour = start; hour < end; hour = hour.AddHours(1))
        {
            var rain = weatherModels
                .Select(m => m.Hours.FirstOrDefault(h => h.LocalTime == hour)?.PrecipitationMm)
                .FirstOrDefault(value => value is not null);
            if (rain is null)
            {
                return null;
            }

            total += rain.Value;
        }

        return Math.Round(total, 1);
    }

    private static List<WeatherHour> DiveWindow(IReadOnlyList<WeatherHour> hours, DateTime start, DateTime end) =>
        hours.Where(h => h.LocalTime >= start && h.LocalTime < end && h.WindSpeedKmh is not null).ToList();

    private static Dictionary<DateOnly, TemperatureProfile> ProfilesAtNoon(DepthTimeSeries series)
    {
        var localDays = series.Times
            .Select(t => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(t, SwissTimeZone).DateTime))
            .Distinct();

        return localDays
            .Select(day => (Day: day, Profile: series.ProfileAt(ToUtc(day, 12))))
            .Where(p => Math.Abs((p.Profile.Time - ToUtc(p.Day, 12)).TotalHours) <= 3)
            .ToDictionary(p => p.Day, p => p.Profile);
    }

    private static CategoryStatus SatelliteStatus(bool productExists, SourceResponse response, RemoteSensingValue? latest, DateOnly today)
    {
        if (!response.IsSuccess)
        {
            return CategoryStatus.Failed;
        }

        if (!productExists || latest is null)
        {
            return CategoryStatus.Missing;
        }

        var ageDays = today.DayNumber - DateOnly.FromDateTime(latest.Time.UtcDateTime).DayNumber;
        return ageDays <= 7 ? CategoryStatus.Ok : CategoryStatus.Partial;
    }

    private void LogSummary(SpikeReport report)
    {
        foreach (var category in report.Categories)
        {
            logger.LogInformation("{Site} | {Category}: {Status} | {Summary}", report.Site, category.Category, category.Status, category.Summary);
        }

        foreach (var day in report.Days)
        {
            logger.LogInformation(
                "{Site} | {Date}: surface {Surface} °C, 10 m {T10} °C, 20 m {T20} °C, 30 m {T30} °C, thermocline {Top}–{Bottom} m, "
                + "wind {Bft} Bft · {Kmh} km/h, gusts {GustBft} Bft · {GustKmh} km/h ({WeatherModel}), rain day {RainDay} mm, rain 72 h before {Rain} mm",
                report.Site, day.Date.ToString("dd.MM.yyyy"), day.SurfaceTemperature, day.TemperatureAt10m, day.TemperatureAt20m, day.TemperatureAt30m,
                day.ThermoclineTop, day.ThermoclineBottom, day.WindBeaufortMax, Round(day.WindSpeedMaxKmh), day.WindGustsBeaufortMax,
                Round(day.WindGustsMaxKmh), day.WeatherModel, day.PrecipitationDayMm, day.PrecipitationLast72hMm);
        }

        logger.LogInformation("Raw responses: {Directory}", report.RawDataDirectory);
    }

    private static DateTimeOffset ToUtc(DateOnly day, int localHour)
    {
        var local = day.ToDateTime(new TimeOnly(localHour, 0));
        return new DateTimeOffset(local, SwissTimeZone.GetUtcOffset(local)).ToUniversalTime();
    }

    private static double? Round(double? value) => value is null ? null : Math.Round(value.Value, 1);

    /// <summary>
    /// The hourly weather of one model.
    /// </summary>
    internal record ModelWeather(string Model, IReadOnlyList<WeatherHour> Hours);

    private static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var sorted = values.Order().ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle];
    }
}
