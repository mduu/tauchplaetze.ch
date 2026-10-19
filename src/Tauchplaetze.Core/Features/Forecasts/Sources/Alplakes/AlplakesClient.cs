using System.Globalization;

namespace Tauchplaetze.Core.Features.Forecasts.Sources.Alplakes;

/// <summary>Client für die Alplakes API der Eawag (`data-sources.md`, «Eawag Alplakes»).</summary>
internal class AlplakesClient(HttpClient httpClient)
{
    public const string Source = "alplakes";

    /// <summary>Sentinel-3, 300 m Pixel. Secchi und Chlorophyll in der Seemitte.</summary>
    public const string Satellite = "sentinel3";

    /// <summary>Sentinel-2, 10 m Pixel. Secchi in Ufernähe (`forecast.md`, «Klarheit»).</summary>
    public const string ShoreSatellite = "sentinel2";

    public const string SecchiVariable = "Zsd_lee";

    /// <summary>Secchi-Tiefe von Sentinel-2 (Annahme: in m wie <see cref="SecchiVariable"/>, Rückfrage an Eawag offen).</summary>
    public const string ShoreSecchiVariable = "Z490";

    public const string ChlorophyllVariable = "chla";

    /// <summary>Temperatur über Tiefe und Zeit am Gitterpunkt nächst der Koordinate (3D-Modell).</summary>
    public Task<SourceResult<DepthTimeSeries>> GetDepthTime(
        string model, string lake, DateTimeOffset start, DateTimeOffset end, double latitude, double longitude, CancellationToken ct) =>
        SourceHttp.Get(
            httpClient,
            $"simulations/depthtime/{model}/{lake}/{FormatTime(start)}/{FormatTime(end)}/{SourceHttp.Format(latitude)}/{SourceHttp.Format(longitude)}",
            json => DepthTimeSeries.Parse(json, "temperature"),
            ct);

    /// <summary>Temperatur über Tiefe und Zeit für den ganzen See (1D-Modell Simstrat).</summary>
    public Task<SourceResult<DepthTimeSeries>> GetLakeWideDepthTime(string lake, DateTimeOffset start, DateTimeOffset end, CancellationToken ct) =>
        SourceHttp.Get(
            httpClient,
            $"simulations/1d/depthtime/simstrat/{lake}/{FormatTime(start)}/{FormatTime(end)}",
            json => DepthTimeSeries.Parse(json, "T"),
            ct);

    /// <summary>Werte eines Satellitenprodukts am Punkt, <paramref name="window"/> Pixel um den Punkt.</summary>
    public Task<SourceResult<IReadOnlyList<SatelliteValue>>> GetSatelliteValues(
        string satellite, string lake, string variable, double latitude, double longitude, DateOnly start, DateOnly end, int window, CancellationToken ct) =>
        SourceHttp.Get(
            httpClient,
            $"remotesensing/timeseries/{lake}/{satellite}/{variable}/{SourceHttp.Format(latitude)}/{SourceHttp.Format(longitude)}/{start:yyyyMMdd}/{end:yyyyMMdd}?window={window}",
            SatelliteValue.ParseList,
            ct);

    /// <summary>Die Satellitenprodukte pro See. Die API leitet auf eine Datei in S3 weiter.</summary>
    public Task<SourceResult<RemoteSensingProducts>> GetRemoteSensingProducts(CancellationToken ct) =>
        SourceHttp.Get(httpClient, "remotesensing/metadata", RemoteSensingProducts.Parse, ct);

    private static string FormatTime(DateTimeOffset time) => time.UtcDateTime.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
}
