using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Forecasts.Calculation;
using Tauchplaetze.Core.Features.Forecasts.Sources;
using Tauchplaetze.Core.Features.Forecasts.Sources.Alplakes;
using Tauchplaetze.Core.Features.Forecasts.Sources.Hydro;
using Tauchplaetze.Core.Features.Forecasts.Sources.OpenMeteo;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.Forecasts;

/// <summary>Die Daten eines Gewässers für einen Lauf.</summary>
internal record WaterBodyData(DepthTimeSeries? LakeWide, double? DischargeRatio, double? RiverTemperature);

/// <summary>Die Daten eines Platzes für einen Lauf.</summary>
internal record SiteData(
    DepthTimeSeries? Model3d,
    IReadOnlyList<ModelWeather> Weather,
    SatelliteObservation? Secchi,
    SatelliteObservation? Chlorophyll,
    SatelliteObservation? ShoreSecchi = null);

/// <summary>Holt die Daten aller Quellen. Plätze und Gewässer mit denselben Abfragen teilen sich den Call.</summary>
internal class ForecastSources(AlplakesClient alplakes, OpenMeteoClient openMeteo, HydroClient hydro)
{
    public const int ForecastDays = 5;

    private const int PastWeatherDays = 3;

    private const int LookbackDays = 30;

    private const int SatelliteWindow = 1;

    /// <summary>Pixel rund um den Punkt in Ufernähe. Ein Fenster von 2 fängt einzelne maskierte Pixel auf.</summary>
    private const int ShoreSatelliteWindow = 2;

    /// <summary>Distanz des Punkts in Ufernähe vom Platz, in Richtung Seemitte.</summary>
    private const double SeawardMeters = 100;

    private Task<RemoteSensingProducts?>? products;

    /// <summary>Wettermodelle nach Vorrang, mit Vorlauf in Tagen (`data-sources.md`, «Wetter: Open-Meteo»).</summary>
    private static readonly (string Model, int Days)[] WeatherModels = [("meteoswiss_icon_ch1", 2), ("meteoswiss_icon_ch2", 5), ("icon_seamless", 7)];

    private readonly Dictionary<string, Task<SatelliteObservation?>> satellite = [];

    private readonly Dictionary<string, Task<IReadOnlyList<ModelWeather>>> weather = [];

    public async Task<WaterBodyData> ForWaterBody(WaterBody waterBody, DateOnly today, SourceRecorder recorder, CancellationToken ct)
    {
        var (start, end) = ModelPeriod(today);
        var lakeWide = waterBody.AlplakesSimstratKey is { } key
            ? await recorder.Record(AlplakesClient.Source, $"1d-{key}", alplakes.GetLakeWideDepthTime(key, start, end, ct), ct)
            : null;

        var ratios = new List<double>();
        double? riverTemperature = null;
        foreach (var station in waterBody.HydroStations)
        {
            var flow = await recorder.Record(HydroClient.Source, $"{station.StationId}-flow",
                hydro.GetRange(station.StationId, "flow", today.AddDays(-LookbackDays), today.AddDays(1), ct), ct);
            if (RatioToMedian(flow) is { } ratio)
            {
                ratios.Add(ratio);
            }

            if (station.Role == HydroStationRole.River)
            {
                var temperature = await recorder.Record(HydroClient.Source, $"{station.StationId}-temperature",
                    hydro.GetRange(station.StationId, "temperature", today.AddDays(-1), today.AddDays(1), ct), ct);
                riverTemperature ??= temperature is [.., var latest] ? latest.Value : null;
            }
        }

        return new WaterBodyData(lakeWide, ratios.Count > 0 ? ratios.Max() : null, riverTemperature);
    }

    public async Task<SiteData> ForSite(DiveSite site, WaterBody waterBody, DateOnly today, SourceRecorder recorder, CancellationToken ct)
    {
        var (start, end) = ModelPeriod(today);
        var model3d = waterBody is { AlplakesModel3d: { } model, AlplakesLakeKey: { } lake }
            ? await recorder.Record(AlplakesClient.Source, $"3d-{model}-{site.Slug}",
                alplakes.GetDepthTime(model, lake, start, end, site.Location.Y, site.Location.X, ct), ct)
            : null;

        var point = site.SatellitePoint ?? waterBody.SatellitePoint ?? site.Location;
        var secchi = waterBody.AlplakesLakeKey is { } key ? await Satellite(AlplakesClient.Satellite, key, AlplakesClient.SecchiVariable, point.Y, point.X, SatelliteWindow, today, recorder, ct) : null;
        var chlorophyll = waterBody.AlplakesLakeKey is { } key2 ? await Satellite(AlplakesClient.Satellite, key2, AlplakesClient.ChlorophyllVariable, point.Y, point.X, SatelliteWindow, today, recorder, ct) : null;

        return new SiteData(model3d, await Weather(site.Location.Y, site.Location.X, recorder, ct), secchi, chlorophyll, await ShoreSecchi(site, waterBody, today, recorder, ct));
    }

    /// <remarks>
    /// Ein eigener Satellitenpunkt des Platzes gilt auch hier. Sonst 100 m vom Platz Richtung Satellitenpunkt des Sees (bzw. Mitte).
    /// Nur ein Versuch pro Platz: Ist dort kein Wert, gilt Sentinel-3.
    /// </remarks>
    private async Task<SatelliteObservation?> ShoreSecchi(DiveSite site, WaterBody waterBody, DateOnly today, SourceRecorder recorder, CancellationToken ct)
    {
        if (waterBody.AlplakesLakeKey is not { } lake
            || await Products(recorder, ct) is not { } available
            || !available.Has(lake, AlplakesClient.ShoreSatellite, AlplakesClient.ShoreSecchiVariable))
        {
            return null;
        }

        var toward = waterBody.SatellitePoint ?? waterBody.Centroid;
        var (latitude, longitude) = site.SatellitePoint is { } own
            ? (own.Y, own.X)
            : SeawardPoint.Of(site.Location.Y, site.Location.X, toward.Y, toward.X, SeawardMeters);
        return await Satellite(AlplakesClient.ShoreSatellite, lake, AlplakesClient.ShoreSecchiVariable, latitude, longitude, ShoreSatelliteWindow, today, recorder, ct);
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
        return values is [var latest, ..] ? new SatelliteObservation(SwissTime.DayOf(latest.Time), latest.Median) : null;
    }

    /// <remarks>Plätze im selben Gitterpunkt teilen sich den Call. Zwei Dezimalen sind rund 1 km, so fein wie ICON-CH1.</remarks>
    private Task<IReadOnlyList<ModelWeather>> Weather(double latitude, double longitude, SourceRecorder recorder, CancellationToken ct)
    {
        var key = $"{Math.Round(latitude, 2):0.00}-{Math.Round(longitude, 2):0.00}";
        if (!weather.TryGetValue(key, out var task))
        {
            task = LoadWeather(latitude, longitude, key, recorder, ct);
            weather[key] = task;
        }

        return task;
    }

    private async Task<IReadOnlyList<ModelWeather>> LoadWeather(double latitude, double longitude, string key, SourceRecorder recorder, CancellationToken ct)
    {
        var models = new List<ModelWeather>();
        foreach (var (model, days) in WeatherModels)
        {
            var hourly = await recorder.Record(OpenMeteoClient.Source, $"{model}-{key}",
                openMeteo.GetHourly(model, latitude, longitude, PastWeatherDays, days, ct), ct);
            if (hourly is not null)
            {
                models.Add(new ModelWeather(model, hourly.Hours));
            }
        }

        return models;
    }

    private static (DateTimeOffset Start, DateTimeOffset End) ModelPeriod(DateOnly today) =>
        (SwissTime.ToUtc(today, 0), SwissTime.ToUtc(today.AddDays(ForecastDays), 0));

    private static double? RatioToMedian(IReadOnlyList<HydroValue>? values)
    {
        if (values is not [.., var latest])
        {
            return null;
        }

        var sorted = values.Select(v => v.Value).Order().ToList();
        var median = sorted[sorted.Count / 2];
        return median > 0 ? Math.Round(latest.Value / median, 2) : null;
    }
}
