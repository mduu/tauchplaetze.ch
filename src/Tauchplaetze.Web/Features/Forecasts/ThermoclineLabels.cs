namespace Tauchplaetze.Web.Features.Forecasts;

/// <summary>Beschriftung der Sprungschicht auf der Tiefenachse: Ober- und Unterkante einzeln, bei einem engen Band eine Beschriftung «12–14 m».</summary>
internal static class ThermoclineLabels
{
    public record Label(double Y, string Text);

    /// <param name="y">Die Tiefe in m als y-Koordinate im Diagramm.</param>
    /// <param name="minGap">Der kleinste Abstand zweier Beschriftungen in Pixeln.</param>
    /// <param name="minY">Keine Beschriftung höher als diese y-Koordinate, damit sie die Oberfläche nicht überdeckt.</param>
    public static IReadOnlyList<Label> For(decimal top, decimal bottom, Func<decimal, double> y, double minGap, double minY = double.MinValue)
    {
        var (upper, lower) = (y(top), y(bottom));
        IReadOnlyList<Label> labels = lower - upper < minGap
            ? [new Label((upper + lower) / 2, $"{ForecastFormat.Meters(top)}–{ForecastFormat.Meters(bottom)} m")]
            : [new Label(upper, $"{ForecastFormat.Meters(top)} m"), new Label(lower, $"{ForecastFormat.Meters(bottom)} m")];
        return [.. labels.Select(label => label with { Y = Math.Max(label.Y, minY) })];
    }

    /// <summary>Die y-Koordinate für den Text unter dem Band: mindestens <paramref name="minGap"/> unter der tiefsten Beschriftung.</summary>
    public static double BelowBand(double bandBottom, IReadOnlyList<Label> labels, double minGap) =>
        Math.Max(bandBottom + minGap, labels.Count == 0 ? double.MinValue : labels.Max(label => label.Y) + minGap);

    /// <summary>Ob ein Achsenwert so nahe an einer Beschriftung der Sprungschicht liegt, dass er sie überdecken würde.</summary>
    public static bool Overlaps(IReadOnlyList<Label> labels, double y, double minGap) =>
        labels.Any(label => Math.Abs(label.Y - y) < minGap);
}
