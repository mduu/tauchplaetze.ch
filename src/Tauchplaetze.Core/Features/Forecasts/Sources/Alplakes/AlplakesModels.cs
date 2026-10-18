using System.Globalization;
using System.Text.Json;
using Tauchplaetze.Core.Features.Forecasts.Calculation;

namespace Tauchplaetze.Core.Features.Forecasts.Sources.Alplakes;

/// <summary>Temperatur über Tiefe und Zeit. <see cref="Temperatures"/> ist [Tiefe, Zeit], <c>null</c> ohne Modellwert.</summary>
internal record DepthTimeSeries(
    IReadOnlyList<DateTimeOffset> Times,
    IReadOnlyList<double> Depths,
    double?[,] Temperatures,
    double? DistanceToGridPoint)
{
    /// <remarks>3D (`temperature`) und 1D (`T`) haben dieselbe Form: Zeilen pro Tiefe, ein Wert pro Zeitschritt. Nur das 3D-Modell kennt `distance`.</remarks>
    public static DepthTimeSeries Parse(string json, string variable)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var times = root.GetProperty("time").EnumerateArray()
            .Select(t => DateTimeOffset.Parse(t.GetString()!, CultureInfo.InvariantCulture))
            .ToList();
        var depths = root.GetProperty("depth").GetProperty("data").EnumerateArray().Select(d => d.GetDouble()).ToList();
        var rows = root.GetProperty("variables").GetProperty(variable).GetProperty("data").EnumerateArray().ToList();

        var temperatures = new double?[depths.Count, times.Count];
        for (var depth = 0; depth < rows.Count; depth++)
        {
            var values = rows[depth].EnumerateArray().ToList();
            for (var time = 0; time < values.Count; time++)
            {
                temperatures[depth, time] = values[time].ValueKind == JsonValueKind.Number ? values[time].GetDouble() : null;
            }
        }

        return new DepthTimeSeries(times, depths, temperatures, ReadDistance(root));
    }

    /// <summary>Das Profil im Zeitschritt nächst <paramref name="time"/>, von der Oberfläche zum Grund. <c>null</c>, wenn kein Zeitschritt innerhalb von <paramref name="tolerance"/> liegt.</summary>
    public TemperatureProfile? ProfileAt(DateTimeOffset time, TimeSpan tolerance)
    {
        if (Times.Count == 0)
        {
            return null;
        }

        var index = Enumerable.Range(0, Times.Count).MinBy(i => Math.Abs((Times[i] - time).Ticks));
        if ((Times[index] - time).Duration() > tolerance)
        {
            return null;
        }

        var points = Enumerable.Range(0, Depths.Count)
            .Where(depth => Temperatures[depth, index] is not null)
            .Select(depth => new ProfilePoint(Depths[depth], Temperatures[depth, index]!.Value))
            .OrderBy(p => p.Depth)
            .ToList();

        return new TemperatureProfile(Times[index], points);
    }

    private static double? ReadDistance(JsonElement root)
    {
        if (!root.TryGetProperty("distance", out var distance))
        {
            return null;
        }

        var data = distance.ValueKind == JsonValueKind.Object ? distance.GetProperty("data") : distance;
        return data.ValueKind == JsonValueKind.Number ? data.GetDouble() : null;
    }
}

/// <summary>Statistik eines Satellitenprodukts in einem Fenster um einen Punkt.</summary>
internal record SatelliteValue(DateTimeOffset Time, double Median, int PixelCount)
{
    public static IReadOnlyList<SatelliteValue> ParseList(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray()
            .Select(e =>
            {
                var value = e.GetProperty("value");
                return new SatelliteValue(
                    DateTimeOffset.Parse(e.GetProperty("time").GetString()!, CultureInfo.InvariantCulture),
                    value.GetProperty("median").GetDouble(),
                    value.GetProperty("count").GetInt32());
            })
            .OrderByDescending(v => v.Time)
            .ToList();
    }
}
