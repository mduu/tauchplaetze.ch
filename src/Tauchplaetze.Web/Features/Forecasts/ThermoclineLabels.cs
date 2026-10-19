namespace Tauchplaetze.Web.Features.Forecasts;

/// <summary>Beschriftung der Sprungschicht auf der Tiefenachse: Ober- und Unterkante einzeln, bei einem engen Band eine Beschriftung «12–14 m».</summary>
internal static class ThermoclineLabels
{
    public record Label(double Y, string Text);

    /// <param name="y">Die Tiefe in m als y-Koordinate im Diagramm.</param>
    /// <param name="minGap">Der kleinste Abstand zweier Beschriftungen in Pixeln.</param>
    public static IReadOnlyList<Label> For(decimal top, decimal bottom, Func<decimal, double> y, double minGap)
    {
        var (upper, lower) = (y(top), y(bottom));
        return lower - upper < minGap
            ? [new Label((upper + lower) / 2, $"{ForecastFormat.Meters(top)}–{ForecastFormat.Meters(bottom)} m")]
            : [new Label(upper, $"{ForecastFormat.Meters(top)} m"), new Label(lower, $"{ForecastFormat.Meters(bottom)} m")];
    }

    /// <summary>Ob ein Achsenwert so nahe an einer Beschriftung der Sprungschicht liegt, dass er sie überdecken würde.</summary>
    public static bool Overlaps(IReadOnlyList<Label> labels, double y, double minGap) =>
        labels.Any(label => Math.Abs(label.Y - y) < minGap);
}
