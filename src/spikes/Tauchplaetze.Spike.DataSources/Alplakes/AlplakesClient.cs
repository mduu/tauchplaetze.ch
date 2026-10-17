using System.Globalization;
using System.Text.Json;

namespace Tauchplaetze.Spike.DataSources.Alplakes;

/// <summary>
/// Typed client for the Eawag Alplakes API (<c>https://alplakes-api.eawag.ch</c>).
/// </summary>
internal class AlplakesClient(HttpClient httpClient, RawResponseStore store)
{
    private const string Source = "alplakes";

    /// <summary>
    /// Gets the metadata of all 3D simulations. The result maps a model to its lakes.
    /// </summary>
    public async Task<(SourceResponse Response, IReadOnlyList<SimulationLake> Lakes)> GetSimulationMetadata(CancellationToken ct)
    {
        var response = await SourceHttp.Get(httpClient, "simulations/metadata", store, Source, "simulations-metadata", ct);
        return (response, response.IsSuccess ? ParseSimulationMetadata(response.Body!) : []);
    }

    /// <summary>
    /// Gets the metadata of all 1D (Simstrat) simulations.
    /// </summary>
    public async Task<(SourceResponse Response, IReadOnlyList<string> Lakes)> GetOneDimensionalLakes(CancellationToken ct)
    {
        var response = await SourceHttp.Get(httpClient, "simulations/1d/metadata", store, Source, "simulations-1d-metadata", ct);
        return (response, response.IsSuccess ? ParseOneDimensionalLakes(response.Body!) : []);
    }

    /// <summary>
    /// Gets the temperature over depth and time at one point (3D model).
    /// </summary>
    public async Task<(SourceResponse Response, DepthTimeSeries? Series)> GetDepthTime(
        string model, string lake, DateTimeOffset start, DateTimeOffset end, double latitude, double longitude, CancellationToken ct)
    {
        var path = $"simulations/depthtime/{model}/{lake}/{FormatTime(start)}/{FormatTime(end)}/{Format(latitude)}/{Format(longitude)}";
        var response = await SourceHttp.Get(httpClient, path, store, Source, $"depthtime-{model}-{lake}", ct);
        return (response, response.IsSuccess ? DepthTimeSeries.Parse(response.Body!) : null);
    }

    /// <summary>
    /// Gets the Simstrat 1D profile over time (lake-wide, no coordinate). Includes the variable <c>Thermocline</c>.
    /// </summary>
    public async Task<SourceResponse> GetOneDimensionalDepthTime(string lake, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var path = $"simulations/1d/depthtime/simstrat/{lake}/{FormatTime(start)}/{FormatTime(end)}";
        return await SourceHttp.Get(httpClient, path, store, Source, $"depthtime-1d-simstrat-{lake}", ct);
    }

    /// <summary>
    /// Gets the directory of remote sensing products per lake and satellite.
    /// </summary>
    public async Task<(SourceResponse Response, IReadOnlyDictionary<string, IReadOnlyList<string>> VariablesBySatellite)> GetRemoteSensingProducts(
        string lake, CancellationToken ct)
    {
        var response = await SourceHttp.Get(httpClient, "remotesensing/metadata", store, Source, "remotesensing-metadata", ct);
        if (!response.IsSuccess)
        {
            return (response, new Dictionary<string, IReadOnlyList<string>>());
        }

        return (response, ParseRemoteSensingDirectory(response.Body!, lake));
    }

    /// <summary>
    /// Gets the list of satellite images of one product since a date.
    /// </summary>
    public async Task<(SourceResponse Response, IReadOnlyList<RemoteSensingImage> Images)> GetRemoteSensingImages(
        string lake, string satellite, string variable, DateOnly since, CancellationToken ct)
    {
        var path = $"remotesensing/products/{lake}/{satellite}/{variable}?start_date={since:yyyyMMdd}";
        var response = await SourceHttp.Get(httpClient, path, store, Source, $"rs-products-{lake}-{satellite}-{variable}", ct);
        return (response, response.IsSuccess ? RemoteSensingImage.ParseList(response.Body!) : []);
    }

    /// <summary>
    /// Gets the satellite values around one point. <paramref name="window"/> is the number of pixels around the point.
    /// </summary>
    public async Task<(SourceResponse Response, IReadOnlyList<RemoteSensingValue> Values)> GetRemoteSensingTimeseries(
        string lake, string satellite, string variable, double latitude, double longitude, DateOnly start, DateOnly end, int window,
        CancellationToken ct)
    {
        var path = $"remotesensing/timeseries/{lake}/{satellite}/{variable}/{Format(latitude)}/{Format(longitude)}/{start:yyyyMMdd}/{end:yyyyMMdd}?window={window}";
        var response = await SourceHttp.Get(httpClient, path, store, Source, $"rs-timeseries-{lake}-{satellite}-{variable}-w{window}", ct);
        return (response, response.IsSuccess ? RemoteSensingValue.ParseList(response.Body!) : []);
    }

    internal static IReadOnlyList<SimulationLake> ParseSimulationMetadata(string json)
    {
        using var document = JsonDocument.Parse(json);
        var lakes = new List<SimulationLake>();
        foreach (var model in document.RootElement.EnumerateArray())
        {
            var modelName = model.GetProperty("model").GetString() ?? "";
            foreach (var lake in model.GetProperty("lakes").EnumerateArray())
            {
                lakes.Add(new SimulationLake(
                    modelName,
                    lake.GetProperty("name").GetString() ?? "",
                    ParseDate(lake, "start_date"),
                    ParseDate(lake, "end_date"),
                    lake.TryGetProperty("missing_dates", out var missing) ? missing.GetArrayLength() : 0));
            }
        }

        return lakes;
    }

    internal static IReadOnlyList<string> ParseOneDimensionalLakes(string json)
    {
        using var document = JsonDocument.Parse(json);
        var lakes = new List<string>();
        foreach (var model in document.RootElement.EnumerateArray())
        {
            foreach (var lake in model.GetProperty("lakes").EnumerateArray())
            {
                if (lake.TryGetProperty("name", out var name) && name.GetString() is { } value)
                {
                    lakes.Add(value);
                }
            }
        }

        return lakes;
    }

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> ParseRemoteSensingDirectory(string json, string lake)
    {
        using var document = JsonDocument.Parse(json);
        var result = new Dictionary<string, IReadOnlyList<string>>();
        if (!document.RootElement.TryGetProperty(lake, out var lakeElement))
        {
            return result;
        }

        foreach (var satellite in lakeElement.EnumerateObject())
        {
            result[satellite.Name] = satellite.Value.EnumerateArray().Select(v => v.GetString() ?? "").ToList();
        }

        return result;
    }

    private static DateOnly? ParseDate(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && DateOnly.TryParse(value.GetString(), CultureInfo.InvariantCulture, out var date)
            ? date
            : null;

    private static string FormatTime(DateTimeOffset time) => time.UtcDateTime.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);

    private static string Format(double value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
}

/// <summary>
/// One lake of one 3D model with the available period.
/// </summary>
public record SimulationLake(string Model, string Lake, DateOnly? StartDate, DateOnly? EndDate, int MissingDates);
