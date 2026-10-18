namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>Die Schicht mit starkem Temperaturabfall, in Metern.</summary>
internal record Thermocline(double Top, double Bottom);

internal static class ThermoclineDetector
{
    /// <summary>
    /// Die Schicht um den stärksten Abfall, in der der Abfall mindestens <paramref name="gradientThreshold"/> °C pro Meter beträgt.
    /// <c>null</c> für einen durchmischten See (`forecast.md`, «Sprungschicht»).
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

        return new Thermocline(points[first].Depth, points[last + 1].Depth);
    }
}
