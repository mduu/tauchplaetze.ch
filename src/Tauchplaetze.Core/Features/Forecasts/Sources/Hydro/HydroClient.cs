using System.Text.Json;

namespace Tauchplaetze.Core.Features.Forecasts.Sources.Hydro;

/// <summary>Client für die BAFU-Hydrodaten über existenz.ch (ADR-007). Messwerte alle 10 min.</summary>
internal class HydroClient(HttpClient httpClient)
{
    public const string Source = "hydro";

    private const string AppName = "tauchplaetze.ch";

    /// <summary>Messwerte einer Station: `flow` (Abfluss), `height` (Pegel) oder `temperature` (Wassertemperatur).</summary>
    public Task<SourceResult<IReadOnlyList<HydroValue>>> GetRange(string stationId, string parameter, DateOnly start, DateOnly end, CancellationToken ct) =>
        SourceHttp.Get(
            httpClient,
            $"apiv1/hydro/daterange?locations={stationId}&parameters={parameter}&startdate={start:yyyy-MM-dd}&enddate={end:yyyy-MM-dd}&app={AppName}",
            HydroValue.ParseList,
            ct);
}

/// <summary>Ein Messwert einer Station.</summary>
internal record HydroValue(DateTimeOffset Time, double Value)
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
            .Select(e => new HydroValue(DateTimeOffset.FromUnixTimeSeconds(e.GetProperty("timestamp").GetInt64()), e.GetProperty("val").GetDouble()))
            .OrderBy(v => v.Time)
            .ToList();
    }
}
