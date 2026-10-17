using System.Globalization;
using System.Text.Json;
using Tauchplaetze.Spike.DataSources.Analysis;

namespace Tauchplaetze.Spike.DataSources.Alplakes;

/// <summary>
/// Temperature over depth and time at one model grid point.
/// </summary>
/// <param name="Times">The time steps in UTC.</param>
/// <param name="Depths">The depth levels in metres, in the order of the source.</param>
/// <param name="Temperatures">Temperature in °C, indexed [depth, time]. <c>null</c> where the model has no value.</param>
/// <param name="GridLatitude">The latitude of the grid point that the model used.</param>
/// <param name="GridLongitude">The longitude of the grid point that the model used.</param>
/// <param name="DistanceToGridPoint">The distance from the requested point to the grid point in metres.</param>
public record DepthTimeSeries(
    IReadOnlyList<DateTimeOffset> Times,
    IReadOnlyList<double> Depths,
    double?[,] Temperatures,
    double GridLatitude,
    double GridLongitude,
    double? DistanceToGridPoint)
{
    /// <summary>
    /// Parses the response of <c>/simulations/depthtime</c>. The temperature data is an array of depth rows,
    /// each row has one value per time step.
    /// </summary>
    public static DepthTimeSeries Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var times = root.GetProperty("time").EnumerateArray()
            .Select(t => DateTimeOffset.Parse(t.GetString()!, CultureInfo.InvariantCulture))
            .ToList();
        var depths = root.GetProperty("depth").GetProperty("data").EnumerateArray()
            .Select(d => d.GetDouble())
            .ToList();

        var temperatures = new double?[depths.Count, times.Count];
        var rows = root.GetProperty("variables").GetProperty("temperature").GetProperty("data");
        var depthIndex = 0;
        foreach (var row in rows.EnumerateArray())
        {
            var timeIndex = 0;
            foreach (var value in row.EnumerateArray())
            {
                temperatures[depthIndex, timeIndex] = value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
                timeIndex++;
            }

            depthIndex++;
        }

        return new DepthTimeSeries(
            times,
            depths,
            temperatures,
            root.GetProperty("lat").GetDouble(),
            root.GetProperty("lng").GetDouble(),
            ReadDistance(root));
    }

    /// <summary>
    /// Gets the profile at the time step that is closest to <paramref name="time"/>.
    /// </summary>
    public TemperatureProfile ProfileAt(DateTimeOffset time)
    {
        var timeIndex = Enumerable.Range(0, Times.Count)
            .MinBy(i => Math.Abs((Times[i] - time).Ticks));

        var points = new List<ProfilePoint>();
        for (var depthIndex = 0; depthIndex < Depths.Count; depthIndex++)
        {
            if (Temperatures[depthIndex, timeIndex] is { } temperature)
            {
                points.Add(new ProfilePoint(Depths[depthIndex], temperature));
            }
        }

        return new TemperatureProfile(Times[timeIndex], points.OrderBy(p => p.Depth).ToList());
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

/// <summary>
/// One satellite image of a remote sensing product.
/// </summary>
/// <param name="Time">The acquisition time in UTC.</param>
/// <param name="ValidPixels">The share of valid lake pixels in percent. Clouds lower this value.</param>
/// <param name="LakeSubsetUrl">The GeoTIFF cropped to the lake.</param>
public record RemoteSensingImage(DateTimeOffset Time, double? ValidPixels, string? LakeSubsetUrl)
{
    public static IReadOnlyList<RemoteSensingImage> ParseList(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray()
            .Select(e => new RemoteSensingImage(
                DateTimeOffset.Parse(e.GetProperty("time").GetString()!, CultureInfo.InvariantCulture),
                ReadPercent(e),
                e.TryGetProperty("lake_subset", out var subset) ? subset.GetString() : null))
            .OrderByDescending(i => i.Time)
            .ToList();
    }

    private static double? ReadPercent(JsonElement element)
    {
        if (!element.TryGetProperty("valid_pixels", out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            return value.GetDouble();
        }

        var text = value.GetString()?.TrimEnd('%');
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) ? percent : null;
    }
}

/// <summary>
/// The statistics of a satellite product in a pixel window around a point.
/// </summary>
public record RemoteSensingValue(DateTimeOffset Time, double Mean, double Median, double Min, double Max, int PixelCount)
{
    public static IReadOnlyList<RemoteSensingValue> ParseList(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray()
            .Select(e =>
            {
                var value = e.GetProperty("value");
                return new RemoteSensingValue(
                    DateTimeOffset.Parse(e.GetProperty("time").GetString()!, CultureInfo.InvariantCulture),
                    value.GetProperty("mean").GetDouble(),
                    value.GetProperty("median").GetDouble(),
                    value.GetProperty("min").GetDouble(),
                    value.GetProperty("max").GetDouble(),
                    value.GetProperty("count").GetInt32());
            })
            .OrderByDescending(v => v.Time)
            .ToList();
    }
}
