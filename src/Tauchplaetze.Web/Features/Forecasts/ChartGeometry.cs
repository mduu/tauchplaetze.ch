using System.Globalization;

namespace Tauchplaetze.Web.Features.Forecasts;

/// <summary>Pfade für Linien-Diagramme in SVG-Koordinaten (`design-system.md`, «Diagramme»).</summary>
internal static class ChartGeometry
{
    public record Point(double X, double Y);

    /// <summary>
    /// Eine glatte Kurve durch alle Punkte (monotone kubische Interpolation nach Fritsch-Carlson).
    /// Zwischen zwei Punkten bleibt sie im Bereich der beiden Werte, schiesst also nie über ein Maximum hinaus.
    /// </summary>
    public static string SmoothPath(IReadOnlyList<Point> points)
    {
        if (points.Count == 0)
        {
            return "";
        }

        if (points.Count == 1)
        {
            return $"M{Number(points[0].X)} {Number(points[0].Y)}h0";
        }

        var tangents = Tangents(points);
        var path = new System.Text.StringBuilder($"M{Number(points[0].X)} {Number(points[0].Y)}");
        for (var i = 0; i < points.Count - 1; i++)
        {
            var (from, to) = (points[i], points[i + 1]);
            var third = (to.X - from.X) / 3;
            path.Append($"C{Number(from.X + third)} {Number(from.Y + tangents[i] * third)} {Number(to.X - third)} {Number(to.Y - tangents[i + 1] * third)} {Number(to.X)} {Number(to.Y)}");
        }

        return path.ToString();
    }

    /// <summary>Die Fläche unter der Kurve bis zur Grundlinie <paramref name="bottom"/>.</summary>
    public static string AreaPath(IReadOnlyList<Point> points, double bottom) =>
        points.Count < 2 ? "" : $"{SmoothPath(points)}L{Number(points[^1].X)} {Number(bottom)}L{Number(points[0].X)} {Number(bottom)}Z";

    /// <summary>Einzelne Punkte als Linien der Länge null. Mit runden Enden erscheinen sie als Kreise, auch wenn das SVG verzerrt skaliert.</summary>
    public static string Dots(IEnumerable<Point> points) =>
        string.Concat(points.Select(p => $"M{Number(p.X)} {Number(p.Y)}h0"));

    public static string Number(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    private static double[] Tangents(IReadOnlyList<Point> points)
    {
        var n = points.Count;
        var slopes = new double[n - 1];
        for (var i = 0; i < n - 1; i++)
        {
            var dx = points[i + 1].X - points[i].X;
            slopes[i] = dx == 0 ? 0 : (points[i + 1].Y - points[i].Y) / dx;
        }

        var tangents = new double[n];
        tangents[0] = slopes[0];
        tangents[n - 1] = slopes[n - 2];
        for (var i = 1; i < n - 1; i++)
        {
            tangents[i] = slopes[i - 1] * slopes[i] <= 0 ? 0 : (slopes[i - 1] + slopes[i]) / 2;
        }

        for (var i = 0; i < n - 1; i++)
        {
            if (slopes[i] == 0)
            {
                tangents[i] = 0;
                tangents[i + 1] = 0;
                continue;
            }

            var (a, b) = (tangents[i] / slopes[i], tangents[i + 1] / slopes[i]);
            var length = Math.Sqrt(a * a + b * b);
            if (length > 3)
            {
                tangents[i] = 3 / length * a * slopes[i];
                tangents[i + 1] = 3 / length * b * slopes[i];
            }
        }

        return tangents;
    }
}
