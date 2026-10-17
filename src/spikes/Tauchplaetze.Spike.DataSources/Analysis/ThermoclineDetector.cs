namespace Tauchplaetze.Spike.DataSources.Analysis;

/// <summary>
/// The layer with a strong temperature drop.
/// </summary>
/// <param name="Top">The upper limit in metres.</param>
/// <param name="Bottom">The lower limit in metres.</param>
/// <param name="MaxGradient">The strongest drop in °C per metre.</param>
public record Thermocline(double Top, double Bottom, double MaxGradient);

/// <summary>
/// Finds the thermocline in a temperature profile (specs/forecast.md, "Sprungschicht").
/// </summary>
internal static class ThermoclineDetector
{
    /// <summary>
    /// Finds the layer around the strongest temperature drop where the drop is at least
    /// <paramref name="gradientThreshold"/> °C per metre. Returns <c>null</c> for a mixed lake.
    /// </summary>
    public static Thermocline? Detect(TemperatureProfile profile, double gradientThreshold)
    {
        var points = profile.Points;
        if (points.Count < 2)
        {
            return null;
        }

        var gradients = Enumerable.Range(1, points.Count - 1)
            .Select(i => (points[i - 1].Temperature - points[i].Temperature) / (points[i].Depth - points[i - 1].Depth))
            .ToList();

        var strongest = Enumerable.Range(0, gradients.Count).MaxBy(i => gradients[i]);
        if (gradients[strongest] < gradientThreshold)
        {
            return null;
        }

        var first = strongest;
        while (first > 0 && gradients[first - 1] >= gradientThreshold)
        {
            first--;
        }

        var last = strongest;
        while (last < gradients.Count - 1 && gradients[last + 1] >= gradientThreshold)
        {
            last++;
        }

        return new Thermocline(points[first].Depth, points[last + 1].Depth, gradients[strongest]);
    }
}
