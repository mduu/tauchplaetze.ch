namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>
/// Die Richtung vom Platz rechtwinklig zum Ufer in den See (`forecast.md`, «Richtung in den See»): zum nächsten Punkt der Tiefenlinie bei <see cref="ContourDepth"/> m.
/// Die Richtung zum Satellitenpunkt des Sees führt oft schräg dem Ufer entlang. Der Punkt in Ufernähe lag dann in den Randpixeln (Zürichsee, 26.10.2026).
/// </summary>
internal static class ShoreNormal
{
    /// <summary>Tiefe der Tiefenlinie in m. Die 5-m-Linie folgt jeder Bucht, die 10-m-Linie zeigt die Richtung ruhiger.</summary>
    public const int ContourDepth = 10;

    /// <summary>So weit darf die Tiefenlinie vom Platz entfernt sein, in m.</summary>
    public const double MaxMeters = 1000;

    private const double MetersPerDegree = 111_320;

    /// <summary>
    /// Ein Punkt <see cref="MaxMeters"/> vom Platz in Richtung des nächsten Punkts der <paramref name="lines"/>. Er dient als Ziel für <see cref="SeawardPoint"/>.
    /// <c>null</c> ohne Linie in Reichweite oder wenn der Platz auf der Linie liegt.
    /// </summary>
    public static (double Latitude, double Longitude)? Toward(
        double latitude, double longitude, IEnumerable<IReadOnlyList<(double Latitude, double Longitude)>> lines)
    {
        var metersPerLongitude = MetersPerDegree * Math.Cos(double.DegreesToRadians(latitude));
        (double East, double North) Local((double Latitude, double Longitude) point) =>
            ((point.Longitude - longitude) * metersPerLongitude, (point.Latitude - latitude) * MetersPerDegree);

        var nearest = lines
            .SelectMany(line => line.Zip(line.Skip(1), (from, to) => NearestOnSegment(Local(from), Local(to))))
            .Select(point => (point.East, point.North, Distance: Math.Sqrt(point.East * point.East + point.North * point.North)))
            .Where(point => point.Distance <= MaxMeters)
            .OrderBy(point => point.Distance)
            .Cast<(double East, double North, double Distance)?>()
            .FirstOrDefault();
        if (nearest is not { Distance: >= 1 } found)
        {
            return null;
        }

        var share = MaxMeters / found.Distance;
        return (latitude + found.North * share / MetersPerDegree, longitude + found.East * share / metersPerLongitude);
    }

    /// <summary>Der Punkt der Strecke, der dem Platz (Ursprung) am nächsten liegt.</summary>
    private static (double East, double North) NearestOnSegment((double East, double North) from, (double East, double North) to)
    {
        var (dx, dy) = (to.East - from.East, to.North - from.North);
        var lengthSquared = dx * dx + dy * dy;
        var share = lengthSquared == 0 ? 0 : Math.Clamp(-(from.East * dx + from.North * dy) / lengthSquared, 0, 1);
        return (from.East + share * dx, from.North + share * dy);
    }
}
