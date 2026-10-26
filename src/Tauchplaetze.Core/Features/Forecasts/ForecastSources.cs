using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Forecasts.Calculation;
using Tauchplaetze.Core.Features.Forecasts.Sources;
using Tauchplaetze.Core.Features.Forecasts.Sources.Alplakes;
using Tauchplaetze.Core.Features.Forecasts.Sources.Bathymetry;
using Tauchplaetze.Core.Features.Forecasts.Sources.Hydro;
using Tauchplaetze.Core.Features.Forecasts.Sources.OpenMeteo;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.Forecasts;

/// <summary>Neuester Abfluss geteilt durch den Median der letzten 30 Tage, und der Median selbst (mindestens <see cref="DischargeRules.MinMedian"/>).</summary>
internal record StationFlowRatio(double Ratio, double Median, DateTimeOffset MeasuredAt);

/// <summary>
/// Die Daten eines Gewässers für einen Lauf. <see cref="Ratios"/>: neuester Abfluss geteilt durch den Median pro Station.
/// <see cref="Medians"/>: Median des Abflusses der letzten 30 Tage pro Station, mindestens <see cref="DischargeRules.MinMedian"/>.
/// </summary>
internal record WaterBodyData(
    DepthTimeSeries? LakeWide,
    IReadOnlyDictionary<string, double> Ratios,
    double? RiverTemperature,
    IReadOnlyDictionary<string, double> Medians,
    IReadOnlyDictionary<string, DateTimeOffset>? MeasuredAt = null);

/// <summary>Die Daten eines Platzes für einen Lauf.</summary>
internal record SiteData(
    DepthTimeSeries? Model3d,
    IReadOnlyList<ModelWeather> Weather,
    SatelliteObservation? Secchi,
    SatelliteObservation? Chlorophyll,
    SatelliteObservation? ShoreSecchi = null);

/// <summary>Holt die Daten aller Quellen. Plätze und Gewässer mit denselben Abfragen teilen sich den Call.</summary>
internal class ForecastSources(
    AlplakesClient alplakes, OpenMeteoClient openMeteo, HydroClient hydro, ContourTiles contours, ILogger<ForecastSources> logger)
{
    public const int ForecastDays = 5;

    private const int PastWeatherDays = 3;

    private const int LookbackDays = 30;

    private const int SatelliteWindow = 1;

    /// <summary>Pixel rund um den Punkt in Ufernähe. Ein Fenster von 2 fängt einzelne maskierte Pixel auf.</summary>
    private const int ShoreSatelliteWindow = 2;

    /// <summary>Gitterpunkte pro Call an Open-Meteo. Die URL bleibt unter 2 KB.</summary>
    private const int WeatherBatchSize = 50;

    private Task<RemoteSensingProducts?>? products;

    /// <summary>Wettermodelle nach Vorrang, mit Vorlauf in Tagen (`data-sources.md`, «Wetter: Open-Meteo»).</summary>
    private static readonly (string Model, int Days)[] WeatherModels = [("meteoswiss_icon_ch1", 2), ("meteoswiss_icon_ch2", 5), ("icon_seamless", 7)];

    private readonly Dictionary<string, Task<SatelliteObservation?>> satellite = [];

    private readonly Dictionary<string, Task<IReadOnlyList<ModelWeather>>> weather = [];

    private readonly Dictionary<string, Task<StationFlowRatio?>> stationFlows = [];

    public async Task<WaterBodyData> ForWaterBody(WaterBody waterBody, DateOnly today, SourceRecorder recorder, CancellationToken ct)
    {
        var (start, end) = ModelPeriod(today);
        var lakeWide = waterBody.AlplakesSimstratKey is { } key
            ? await recorder.Record(AlplakesClient.Source, $"1d-{key}", alplakes.GetLakeWideDepthTime(key, start, end, ct), ct)
            : null;

        var ratios = new Dictionary<string, double>();
        var medians = new Dictionary<string, double>();
        var measuredAt = new Dictionary<string, DateTimeOffset>();
        double? riverTemperature = null;
        foreach (var station in waterBody.HydroStations)
        {
            if (await StationFlow(station.StationId, today, recorder, ct) is { } flow)
            {
                ratios[station.StationId] = flow.Ratio;
                medians[station.StationId] = flow.Median;
                measuredAt[station.StationId] = flow.MeasuredAt;
            }

            if (station.Role == HydroStationRole.River)
            {
                var temperature = await recorder.Record(HydroClient.Source, $"{station.StationId}-temperature",
                    hydro.GetRange(station.StationId, "temperature", today.AddDays(-1), today.AddDays(1), ct), ct);
                riverTemperature ??= temperature is [.., var latest] ? latest.Value : null;
            }
        }

        return new WaterBodyData(lakeWide, ratios, riverTemperature, medians, measuredAt);
    }

    /// <summary>
    /// Quotient und Median einer Station, die nicht am Gewässer hängt, etwa die eingetragene Station eines Flussplatzes.
    /// Pro Lauf höchstens ein Call pro Station.
    /// </summary>
    public async Task<StationFlowRatio?> StationFlow(string stationId, DateOnly today, SourceRecorder recorder, CancellationToken ct)
    {
        if (!stationFlows.TryGetValue(stationId, out var task))
        {
            task = LoadStationFlow(stationId, today, recorder, ct);
            stationFlows[stationId] = task;
        }

        return await task;
    }

    private async Task<StationFlowRatio?> LoadStationFlow(string stationId, DateOnly today, SourceRecorder recorder, CancellationToken ct)
    {
        var flow = await recorder.Record(HydroClient.Source, $"{stationId}-flow",
            hydro.GetRange(stationId, "flow", today.AddDays(-LookbackDays), today.AddDays(1), ct), ct);
        if (flow is not [.., var newest])
        {
            return null;
        }

        var median = Median(flow);
        return new StationFlowRatio(Math.Round(newest.Value / median, 2), median, newest.Time);
    }

    public async Task<SiteData> ForSite(DiveSite site, WaterBody waterBody, DateOnly today, SourceRecorder recorder, CancellationToken ct)
    {
        var model3d = await Model3d(site, waterBody, today, recorder, ct);
        var (secchi, chlorophyll) = await LakeSatellite(site, waterBody, today, recorder, ct);

        return new SiteData(model3d, await Weather(site.Location.Y, site.Location.X, recorder, ct), secchi, chlorophyll, await ShoreSecchi(site, waterBody, today, recorder, ct));
    }

    /// <remarks>
    /// Reicht das Profil am Platz nicht bis <see cref="ModelPoint.MinDepth"/>, folgen Punkte 100 m und 250 m Richtung Seemitte.
    /// Die Richtung bestimmt derselbe Punkt wie bei Sentinel-2. Es gilt das tiefste Profil. Höchstens drei Calls pro Platz.
    /// </remarks>
    private async Task<DepthTimeSeries?> Model3d(DiveSite site, WaterBody waterBody, DateOnly today, SourceRecorder recorder, CancellationToken ct)
    {
        if (waterBody is not { AlplakesModel3d: { } model, AlplakesLakeKey: { } lake })
        {
            return null;
        }

        var (start, end) = ModelPeriod(today);
        var best = await recorder.Record(AlplakesClient.Source, $"3d-{model}-{site.Slug}",
            alplakes.GetDepthTime(model, lake, start, end, site.Location.Y, site.Location.X, ct), ct);
        var toward = Toward(site, waterBody);
        foreach (var meters in ModelPoint.SeawardMeters)
        {
            if (!ModelPoint.TooShallow(best))
            {
                break;
            }

            var point = SeawardPoint.Of(site.Location.Y, site.Location.X, toward.Y, toward.X, meters);
            var seaward = await recorder.Record(AlplakesClient.Source, $"3d-{model}-{site.Slug}-{meters:0}m",
                alplakes.GetDepthTime(model, lake, start, end, point.Latitude, point.Longitude, ct), ct);
            best = ModelPoint.Deeper(best, seaward is null ? null : ModelPoint.FromSite(seaward, meters));
        }

        if (ModelPoint.TooShallow(best))
        {
            logger.LogInformation("3D model profile for dive site {Slug} ends at {Depth} m", site.Slug, best?.DeepestValue);
        }

        return best;
    }

    /// <summary>
    /// Das Ziel für die Punkte Richtung See: rechtwinklig zum Ufer, zum nächsten Punkt der 10-m-Tiefenlinie (<see cref="ShoreNormal"/>).
    /// Ohne Tiefenlinien und an Bootsplätzen der eigene Satellitenpunkt des Platzes, sonst der des Sees, sonst seine Mitte.
    /// </summary>
    private Point Toward(DiveSite site, WaterBody waterBody)
    {
        if (site.Access != DiveSiteAccess.Boat
            && ShoreNormal.Toward(site.Location.Y, site.Location.X, contours.Near(waterBody.Slug, site.Location.Y, site.Location.X, ShoreNormal.ContourDepth, ShoreNormal.MaxMeters))
                is var (latitude, longitude))
        {
            return new Point(longitude, latitude) { SRID = site.Location.SRID };
        }

        return site.SatellitePoint ?? waterBody.SatellitePoint ?? waterBody.Centroid;
    }

    /// <remarks>
    /// Zuerst 50 m vom Platz in den See, bei fehlendem, zu altem Wert oder zu wenigen Pixeln 100 m. Liefern beide nichts, folgen Punkte bei 100 m in gedrehter Richtung (<see cref="ShorePoint.FallbackTurns"/>), bis einer einen brauchbaren Wert hat.
    /// Höchstens sechs Calls pro Platz.
    /// Die Richtung bestimmt <see cref="Toward"/>.
    /// Ist kein Wert da, gilt Sentinel-3.
    /// </remarks>
    private async Task<SatelliteObservation?> ShoreSecchi(DiveSite site, WaterBody waterBody, DateOnly today, SourceRecorder recorder, CancellationToken ct)
    {
        if (waterBody.AlplakesLakeKey is not { } lake
            || await Products(recorder, ct) is not { } available
            || !available.Has(lake, AlplakesClient.ShoreSatellite, AlplakesClient.ShoreSecchiVariable))
        {
            return null;
        }

        var toward = Toward(site, waterBody);
        var nearPoint = SeawardPoint.Of(site.Location.Y, site.Location.X, toward.Y, toward.X, ShorePoint.NearMeters);
        var near = await ShoreSatelliteAt(lake, nearPoint.Latitude, nearPoint.Longitude, today, recorder, ct);
        var freshDays = DiveIndexRules.Default.SatelliteFullAgeDays;
        if (!ShorePoint.NeedsFallback(near, today, ShorePoint.MinPixels, freshDays))
        {
            return near;
        }

        var farPoint = SeawardPoint.Of(site.Location.Y, site.Location.X, toward.Y, toward.X, ShorePoint.FarMeters);
        var far = await ShoreSatelliteAt(lake, farPoint.Latitude, farPoint.Longitude, today, recorder, ct);
        var picked = ShorePoint.Pick(near, far, ShorePoint.MinPixels);
        if (picked is not null)
        {
            return picked;
        }

        foreach (var degrees in ShorePoint.FallbackTurns)
        {
            var turnedPoint = SeawardPoint.Turned(site.Location.Y, site.Location.X, toward.Y, toward.X, ShorePoint.FarMeters, degrees);
            var turned = await ShoreSatelliteAt(lake, turnedPoint.Latitude, turnedPoint.Longitude, today, recorder, ct);
            picked = ShorePoint.Pick(picked, turned, ShorePoint.MinPixels);
            if (!ShorePoint.NeedsFallback(picked, today, ShorePoint.MinPixels, freshDays))
            {
                return picked;
            }
        }

        if (picked is null)
        {
            logger.LogInformation("Sentinel-2 has no value around dive site {Slug}", site.Slug);
        }

        return picked;
    }

    private Task<SatelliteObservation?> ShoreSatelliteAt(string lake, double latitude, double longitude, DateOnly today, SourceRecorder recorder, CancellationToken ct) =>
        Satellite(AlplakesClient.ShoreSatellite, lake, AlplakesClient.ShoreSecchiVariable, latitude, longitude, ShoreSatelliteWindow, today, recorder, ct);

    /// <summary>
    /// Secchi und Chlorophyll von Sentinel-3 (300 m Pixel) am Satellitenpunkt im offenen Wasser, nur wenn Alplakes das Produkt für den See führt.
    /// Ohne Satellitenpunkt fallen die Calls weg: Am Ufer liefert Sentinel-3 nichts.
    /// </summary>
    private async Task<(SatelliteObservation? Secchi, SatelliteObservation? Chlorophyll)> LakeSatellite(
        DiveSite site, WaterBody waterBody, DateOnly today, SourceRecorder recorder, CancellationToken ct)
    {
        if (waterBody.AlplakesLakeKey is not { } lake
            || (site.SatellitePoint ?? waterBody.SatellitePoint) is not { } point
            || await Products(recorder, ct) is not { } available
            || !available.Has(lake, AlplakesClient.Satellite, AlplakesClient.SecchiVariable))
        {
            return (null, null);
        }

        return (
            await Satellite(AlplakesClient.Satellite, lake, AlplakesClient.SecchiVariable, point.Y, point.X, SatelliteWindow, today, recorder, ct),
            await Satellite(AlplakesClient.Satellite, lake, AlplakesClient.ChlorophyllVariable, point.Y, point.X, SatelliteWindow, today, recorder, ct));
    }

    /// <summary>Einmal pro Lauf: welche Seen welche Satellitenprodukte haben.</summary>
    private Task<RemoteSensingProducts?> Products(SourceRecorder recorder, CancellationToken ct) =>
        products ??= recorder.Record(AlplakesClient.Source, "remotesensing-metadata", alplakes.GetRemoteSensingProducts(ct), ct);

    private Task<SatelliteObservation?> Satellite(
        string satelliteName, string lake, string variable, double latitude, double longitude, int window, DateOnly today, SourceRecorder recorder, CancellationToken ct)
    {
        var key = $"{satelliteName}-{lake}-{variable}-{SourceHttp.Format(latitude)}-{SourceHttp.Format(longitude)}";
        if (!satellite.TryGetValue(key, out var task))
        {
            task = LoadSatellite(satelliteName, lake, variable, latitude, longitude, window, today, key, recorder, ct);
            satellite[key] = task;
        }

        return task;
    }

    private async Task<SatelliteObservation?> LoadSatellite(
        string satelliteName, string lake, string variable, double latitude, double longitude, int window, DateOnly today, string key, SourceRecorder recorder, CancellationToken ct)
    {
        var values = await recorder.Record(AlplakesClient.Source, $"satellite-{key}",
            alplakes.GetSatelliteValues(satelliteName, lake, variable, latitude, longitude, today.AddDays(-LookbackDays), today, window, ct), ct);
        return values is [var latest, ..] ? new SatelliteObservation(SwissTime.DayOf(latest.Time), latest.Median, latest.PixelCount) : null;
    }

    /// <summary>
    /// Holt das Wetter aller Plätze vorab, pro Modell mit einem Call für bis zu <see cref="WeatherBatchSize"/> Gitterpunkte (`data-sources.md`, «Wetter: Open-Meteo»).
    /// Die abgefragten Punkte liegen pro Call als Beilage bei den Rohdaten.
    /// </summary>
    public async Task PrepareWeather(IEnumerable<DiveSite> sites, SourceRecorder recorder, CancellationToken ct)
    {
        var points = sites.Select(s => WeatherPoint.Of(s.Location.Y, s.Location.X)).Distinct().Where(p => !weather.ContainsKey(p.Key)).ToList();
        var loaded = points.ToDictionary(p => p.Key, _ => new List<ModelWeather>());
        foreach (var (model, days) in WeatherModels)
        {
            foreach (var (batch, number) in points.Chunk(WeatherBatchSize).Select((batch, i) => (batch, i + 1)))
            {
                var key = $"{model}-batch-{number:000}";
                await recorder.Attach(OpenMeteoClient.Source, $"{key}.points", JsonSerializer.Serialize(batch.Select(p => p.Key)), ct);
                var hourly = await recorder.Record(OpenMeteoClient.Source, key,
                    openMeteo.GetHourly(model, [.. batch.Select(p => (p.Latitude, p.Longitude))], PastWeatherDays, days, ct), ct);
                foreach (var (point, values) in batch.Zip(hourly ?? []))
                {
                    loaded[point.Key].Add(new ModelWeather(model, values.Hours));
                }
            }
        }

        foreach (var (key, models) in loaded)
        {
            weather[key] = Task.FromResult<IReadOnlyList<ModelWeather>>(models);
        }
    }

    private async Task<IReadOnlyList<ModelWeather>> Weather(double latitude, double longitude, SourceRecorder recorder, CancellationToken ct)
    {
        var point = WeatherPoint.Of(latitude, longitude);
        if (!weather.TryGetValue(point.Key, out var task))
        {
            task = LoadWeather(point, recorder, ct);
            weather[point.Key] = task;
        }

        return await task;
    }

    private async Task<IReadOnlyList<ModelWeather>> LoadWeather(WeatherPoint point, SourceRecorder recorder, CancellationToken ct)
    {
        var models = new List<ModelWeather>();
        foreach (var (model, days) in WeatherModels)
        {
            var hourly = await recorder.Record(OpenMeteoClient.Source, $"{model}-{point.Key}",
                openMeteo.GetHourly(model, [(point.Latitude, point.Longitude)], PastWeatherDays, days, ct), ct);
            if (hourly is [var values])
            {
                models.Add(new ModelWeather(model, values.Hours));
            }
        }

        return models;
    }

    private static (DateTimeOffset Start, DateTimeOffset End) ModelPeriod(DateOnly today) =>
        (SwissTime.ToUtc(today, 0), SwissTime.ToUtc(today.AddDays(ForecastDays), 0));

    private static double Median(IReadOnlyList<HydroValue> values)
    {
        var sorted = values.Select(v => v.Value).Order().ToList();
        return Math.Max(sorted[sorted.Count / 2], (double)DischargeRules.MinMedian);
    }
}

/// <summary>Ein Gitterpunkt des Wetters. Plätze im selben Punkt teilen sich die Werte. Zwei Dezimalen sind rund 1 km, so fein wie ICON-CH1.</summary>
internal record WeatherPoint(double Latitude, double Longitude)
{
    public string Key => FormattableString.Invariant($"{Latitude:0.00}-{Longitude:0.00}");

    public static WeatherPoint Of(double latitude, double longitude) => new(Math.Round(latitude, 2), Math.Round(longitude, 2));
}
