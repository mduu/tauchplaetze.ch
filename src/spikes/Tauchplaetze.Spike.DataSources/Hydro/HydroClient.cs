using System.Text.Json;

namespace Tauchplaetze.Spike.DataSources.Hydro;

/// <summary>
/// Typed client for BAFU hydrology data through the open API of existenz.ch (<c>https://api.existenz.ch</c>).
/// </summary>
/// <remarks>
/// The Alplakes API has no BAFU proxy any more. existenz.ch republishes the BAFU open data (10-minute values) as JSON.
/// </remarks>
internal class HydroClient(HttpClient httpClient, RawResponseStore store)
{
    private const string AppName = "tauchplaetze.ch-spike";

    public async Task<(SourceResponse Response, IReadOnlyList<HydroValue> Values)> GetRange(
        string stationId, string parameter, DateOnly start, DateOnly end, CancellationToken ct)
    {
        var path = $"apiv1/hydro/daterange?locations={stationId}&parameters={parameter}"
                   + $"&startdate={start:yyyy-MM-dd}&enddate={end:yyyy-MM-dd}&app={AppName}";
        var response = await SourceHttp.Get(httpClient, path, store, "hydro", $"daterange-{stationId}-{parameter}", ct);
        return (response, response.IsSuccess ? HydroValue.ParseList(response.Body!) : []);
    }
}

/// <summary>
/// One measured value of a station.
/// </summary>
public record HydroValue(DateTimeOffset Time, string StationId, string Parameter, double Value)
{
    public static IReadOnlyList<HydroValue> ParseList(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return payload.EnumerateArray()
            .Where(e => e.GetProperty("val").ValueKind == JsonValueKind.Number)
            .Select(e => new HydroValue(
                DateTimeOffset.FromUnixTimeSeconds(e.GetProperty("timestamp").GetInt64()),
                e.GetProperty("loc").GetString() ?? "",
                e.GetProperty("par").GetString() ?? "",
                e.GetProperty("val").GetDouble()))
            .OrderBy(v => v.Time)
            .ToList();
    }
}
