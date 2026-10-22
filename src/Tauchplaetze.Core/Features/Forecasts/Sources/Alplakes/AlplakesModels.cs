using System.Globalization;
using System.Text.Json;
using Tauchplaetze.Core.Features.Forecasts.Calculation;

namespace Tauchplaetze.Core.Features.Forecasts.Sources.Alplakes;

/// <summary>
/// Temperatur über Tiefe und Zeit. <see cref="Temperatures"/> ist [Tiefe, Zeit], <c>null</c> ohne Modellwert.
/// Das 3D-Modell liefert zusätzlich die Strömung nach Osten (<see cref="EastVelocity"/>, `u`) und nach Norden (<see cref="NorthVelocity"/>, `v`) in m/s.
/// </summary>
internal record DepthTimeSeries(
    IReadOnlyList<DateTimeOffset> Times,
    IReadOnlyList<double> Depths,
    double?[,] Temperatures,
    double? DistanceToGridPoint,
    double?[,]? EastVelocity = null,
    double?[,]? NorthVelocity = null)
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
        var variables = root.GetProperty("variables");

        return new DepthTimeSeries(
            times,
            depths,
            ReadGrid(variables, variable, depths.Count, times.Count)!,
            ReadDistance(root),
            ReadGrid(variables, "u", depths.Count, times.Count),
            ReadGrid(variables, "v", depths.Count, times.Count));
    }

    /// <summary>Werte [Tiefe, Zeit] einer Variable, <c>null</c> ohne Modellwert. Fehlt die Variable, ist das Ergebnis <c>null</c>, ausser es ist die verlangte Hauptvariable.</summary>
    private static double?[,]? ReadGrid(JsonElement variables, string name, int depthCount, int timeCount)
    {
        if (!variables.TryGetProperty(name, out var variable) || !variable.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return name is "u" or "v" ? null : throw new JsonException($"Variable {name} fehlt.");
        }

        var grid = new double?[depthCount, timeCount];
        var rows = data.EnumerateArray().ToList();
        for (var depth = 0; depth < Math.Min(rows.Count, depthCount); depth++)
        {
            if (rows[depth].ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var values = rows[depth].EnumerateArray().ToList();
            for (var time = 0; time < Math.Min(values.Count, timeCount); time++)
            {
                grid[depth, time] = values[time].ValueKind == JsonValueKind.Number ? values[time].GetDouble() : null;
            }
        }

        return grid;
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

    /// <summary>Die tiefste Modellschicht mit einem Wert. <c>null</c> ohne Werte.</summary>
    public double? DeepestValue => Enumerable.Range(0, Depths.Count)
        .Where(depth => Enumerable.Range(0, Times.Count).Any(time => Temperatures[depth, time] is not null))
        .Select(depth => (double?)Depths[depth])
        .Max();

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
    /// <summary>Die Werte, neuester zuerst. Ein Bild ohne Median (z.B. <c>null</c>) fällt weg.</summary>
    public static IReadOnlyList<SatelliteValue> ParseList(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray()
            .Select(e =>
            {
                var value = e.GetProperty("value");
                if (!value.TryGetProperty("median", out var median) || median.ValueKind != JsonValueKind.Number)
                {
                    return null;
                }

                return new SatelliteValue(
                    DateTimeOffset.Parse(e.GetProperty("time").GetString()!, CultureInfo.InvariantCulture),
                    median.GetDouble(),
                    value.TryGetProperty("count", out var count) && count.ValueKind == JsonValueKind.Number ? count.GetInt32() : 0);
            })
            .OfType<SatelliteValue>()
            .OrderByDescending(v => v.Time)
            .ToList();
    }
}

/// <summary>Welche Produkte ein Satellit pro See liefert (`remotesensing/metadata`).</summary>
internal record RemoteSensingProducts(IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> Lakes)
{
    public bool Has(string lake, string satellite, string variable) =>
        Lakes.TryGetValue(lake, out var satellites) && satellites.TryGetValue(satellite, out var variables) && variables.Contains(variable);

    public static RemoteSensingProducts Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var lakes = document.RootElement.EnumerateObject().ToDictionary(
            lake => lake.Name,
            lake => (IReadOnlyDictionary<string, IReadOnlyList<string>>)lake.Value.EnumerateObject()
                .Where(p => p.Value.ValueKind == JsonValueKind.Array)
                .ToDictionary(p => p.Name, p => (IReadOnlyList<string>)[.. p.Value.EnumerateArray().Select(v => v.GetString() ?? "")]));
        return new RemoteSensingProducts(lakes);
    }
}
