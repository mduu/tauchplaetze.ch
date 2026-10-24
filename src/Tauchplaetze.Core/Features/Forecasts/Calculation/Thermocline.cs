namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>Die Schicht mit starkem Temperaturabfall, in Metern.</summary>
internal record Thermocline(double Top, double Bottom)
{
    /// <summary>
    /// Eine Sprungschicht an der Oberfläche oder dünner als <paramref name="minThickness"/> ist keine (`rules-1.6`).
    /// So erkennt eine gespeicherte Sprungschicht aus der warmen Haut der obersten Dezimeter nicht mehr als echte.
    /// </summary>
    public Thermocline? Plausible(double minDepth, double minThickness) =>
        Top >= minDepth && Bottom - Top >= minThickness ? this : null;
}

/// <summary>
/// Das Ergebnis der Suche nach der Sprungschicht. <see cref="Layer"/>: die plausible Sprungschicht.
/// <see cref="Stored"/>: was gespeichert wird, auch eine unplausible Schicht. Ist sie gespeichert, aber nicht plausibel, gilt sie als «nicht bestimmt» (`rules-1.7`).
/// Beide <c>null</c>: durchmischter See.
/// </summary>
internal record ThermoclineFinding(Thermocline? Layer, Thermocline? Stored)
{
    /// <summary>Kein Profil unter 1 m oder nur zwei Punkte: Die Sprungschicht lässt sich nicht bestimmen. Gespeichert als 0–0 m.</summary>
    public static readonly Thermocline NotDetermined = new(0, 0);

    public static readonly ThermoclineFinding Mixed = new(null, null);

    public bool Undetermined => Layer is null && Stored is not null;

    public static ThermoclineFinding Of(Thermocline? stored, double minDepth, double minThickness) =>
        stored is null ? Mixed : new(stored.Plausible(minDepth, minThickness), stored);
}

internal static class ThermoclineDetector
{
    /// <summary>Die plausible Sprungschicht oder <c>null</c> (durchmischt oder nicht bestimmt).</summary>
    public static Thermocline? Detect(TemperatureProfile profile, double gradientThreshold, double minDepth = 1, double minThickness = 1) =>
        Find(profile, gradientThreshold, minDepth, minThickness).Layer;

    /// <summary>
    /// Die Schicht um den stärksten Abfall, in der der Abfall mindestens <paramref name="gradientThreshold"/> °C pro Meter beträgt.
    /// Punkte über <paramref name="minDepth"/> zählen nicht: Die Sonne erwärmt tagsüber die obersten Dezimeter (`rules-1.6`).
    /// Ohne Abfall über der Schwelle ist der See durchmischt (`forecast.md`, «Sprungschicht»).
    /// Fehlen Punkte unter <paramref name="minDepth"/> oder ist die Schicht unplausibel, ist sie nicht bestimmt (`rules-1.7`).
    /// </summary>
    public static ThermoclineFinding Find(TemperatureProfile profile, double gradientThreshold, double minDepth = 1, double minThickness = 1)
    {
        var points = profile.Points.Where(p => p.Depth >= minDepth).ToList();
        if (points.Count < 2)
        {
            return new ThermoclineFinding(null, ThermoclineFinding.NotDetermined);
        }

        var gradients = Enumerable.Range(1, points.Count - 1)
            .Select(i => (points[i - 1].Temperature - points[i].Temperature) / (points[i].Depth - points[i - 1].Depth))
            .ToList();

        var strongest = Enumerable.Range(0, gradients.Count).MaxBy(i => gradients[i]);
        if (gradients[strongest] < gradientThreshold)
        {
            return ThermoclineFinding.Mixed;
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

        return ThermoclineFinding.Of(new Thermocline(points[first].Depth, points[last + 1].Depth), minDepth, minThickness);
    }
}
