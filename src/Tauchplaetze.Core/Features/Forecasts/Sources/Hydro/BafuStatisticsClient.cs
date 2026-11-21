using System.Globalization;
using System.Text.Json;

namespace Tauchplaetze.Core.Features.Forecasts.Sources.Hydro;

/// <summary>
/// Client für die Statistik einer Station auf hydrodaten.admin.ch: das JSON der Grafik «Jahresverlauf» (`data-sources.md`, «Seepegel»).
/// Eine Datei pro Station.
/// </summary>
internal class BafuStatisticsClient(HttpClient httpClient)
{
    public const string Source = "bafu-statistics";

    public Task<SourceResult<LakeLevelStatistics>> GetAnnual(string stationId, CancellationToken ct) =>
        SourceHttp.Get(
            httpClient,
            $"plots/p_annual/{Uri.EscapeDataString(stationId)}_p_annual_de.json",
            LakeLevelStatistics.Parse,
            ct);
}

/// <summary>Der übliche Pegel an einem Kalendertag in m ü. M.: 5. Perzentil, Median und 95. Perzentil der langjährigen Tageswerte.</summary>
internal record LakeLevelNorm(decimal Low, decimal Median, decimal High);

/// <summary>Die Statistik einer Seestation pro Kalendertag. Das BAFU zeichnet sie auf das laufende Jahr, darum zählen nur Monat und Tag.</summary>
internal record LakeLevelStatistics(IReadOnlyDictionary<(int Month, int Day), LakeLevelNorm> Days)
{
    private const string BandTrace = "05.-95. Perzentil";

    private const string MedianTrace = "Median";

    public LakeLevelNorm? For(DateOnly day) => Days.GetValueOrDefault((day.Month, day.Day));

    /// <remarks>
    /// Das Band ist ein Polygon: dieselben Tage einmal mit der oberen und einmal mit der unteren Grenze. Pro Tag ist der kleinere Wert das 5., der grössere das 95. Perzentil.
    /// Fehlt das Band (Station ohne Statistik), ist die Statistik leer.
    /// </remarks>
    public static LakeLevelStatistics Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var traces = document.RootElement.GetProperty("plot").GetProperty("data").EnumerateArray().ToList();
        var band = Points(traces, BandTrace)
            .GroupBy(p => p.Day)
            .Where(g => g.Count() == 2)
            .ToDictionary(g => g.Key, g => (Low: g.Min(p => p.Value), High: g.Max(p => p.Value)));
        var median = Points(traces, MedianTrace).GroupBy(p => p.Day).ToDictionary(g => g.Key, g => g.First().Value);
        return new LakeLevelStatistics(band
            .Where(b => median.ContainsKey(b.Key))
            .ToDictionary(b => b.Key, b => new LakeLevelNorm(b.Value.Low, median[b.Key], b.Value.High)));
    }

    private static IEnumerable<((int Month, int Day) Day, decimal Value)> Points(List<JsonElement> traces, string name)
    {
        var trace = traces.FirstOrDefault(t => t.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String && n.GetString() == name);
        if (trace.ValueKind != JsonValueKind.Object
            || !trace.TryGetProperty("x", out var x) || x.ValueKind != JsonValueKind.Array
            || !trace.TryGetProperty("y", out var y) || y.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return x.EnumerateArray().Zip(y.EnumerateArray())
            .Where(p => p.First.ValueKind == JsonValueKind.String && p.Second.ValueKind == JsonValueKind.Number)
            .Select(p => (Date: DateOnly.ParseExact(p.First.GetString()![..10], "yyyy-MM-dd", CultureInfo.InvariantCulture), Value: p.Second.GetDecimal()))
            .Select(p => ((p.Date.Month, p.Date.Day), p.Value))
            .ToList();
    }
}
