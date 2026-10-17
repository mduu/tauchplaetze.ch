namespace Tauchplaetze.Spike.DataSources.Analysis;

/// <summary>
/// One temperature value at one depth.
/// </summary>
public record ProfilePoint(double Depth, double Temperature);

/// <summary>
/// A temperature profile at one time, sorted from the surface to the bottom.
/// </summary>
public record TemperatureProfile(DateTimeOffset Time, IReadOnlyList<ProfilePoint> Points)
{
    /// <summary>
    /// Gets the temperature at a depth with linear interpolation. Returns <c>null</c> outside the profile.
    /// </summary>
    public double? TemperatureAt(double depth)
    {
        if (Points.Count == 0 || depth < Points[0].Depth - 0.5 || depth > Points[^1].Depth)
        {
            return null;
        }

        if (depth <= Points[0].Depth)
        {
            return Points[0].Temperature;
        }

        for (var i = 1; i < Points.Count; i++)
        {
            var upper = Points[i - 1];
            var lower = Points[i];
            if (depth <= lower.Depth)
            {
                var share = (depth - upper.Depth) / (lower.Depth - upper.Depth);
                return upper.Temperature + share * (lower.Temperature - upper.Temperature);
            }
        }

        return null;
    }
}
