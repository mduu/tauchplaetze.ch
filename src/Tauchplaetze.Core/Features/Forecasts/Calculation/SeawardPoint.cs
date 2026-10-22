namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>
/// Der Punkt für den Satelliten in Ufernähe (`forecast.md`, «Klarheit»): eine feste Distanz vom Platz in Richtung Seemitte.
/// Auf der Koordinate eines Platzes am Ufer ist das Pixel von Sentinel-2 meist als Land maskiert.
/// </summary>
internal static class SeawardPoint
{
    private const double MetersPerDegree = 111_320;

    /// <summary>
    /// Rund <paramref name="meters"/> vom Platz zum Punkt <paramref name="towardLatitude"/>/<paramref name="towardLongitude"/> (Satellitenpunkt oder Mitte des Sees).
    /// Liegt das Ziel näher, gilt das Ziel. Fällt es mit dem Platz zusammen, gilt der Platz.
    /// </summary>
    public static (double Latitude, double Longitude) Of(double latitude, double longitude, double towardLatitude, double towardLongitude, double meters)
    {
        var metersPerLongitude = MetersPerDegree * Math.Cos(double.DegreesToRadians(latitude));
        var north = (towardLatitude - latitude) * MetersPerDegree;
        var east = (towardLongitude - longitude) * metersPerLongitude;
        var distance = Math.Sqrt(north * north + east * east);
        if (distance < 1)
        {
            return (latitude, longitude);
        }

        var share = Math.Min(1, meters / distance);
        return (Math.Round(latitude + north * share / MetersPerDegree, 5), Math.Round(longitude + east * share / metersPerLongitude, 5));
    }

    /// <summary>
    /// Wie <see cref="Of"/>, aber um <paramref name="degrees"/> im Uhrzeigersinn gedreht. Die Distanz gilt immer voll.
    /// Für Plätze, an denen die Richtung zur Seemitte dem Ufer entlang führt.
    /// </summary>
    public static (double Latitude, double Longitude) Turned(double latitude, double longitude, double towardLatitude, double towardLongitude, double meters, double degrees)
    {
        var metersPerLongitude = MetersPerDegree * Math.Cos(double.DegreesToRadians(latitude));
        var north = (towardLatitude - latitude) * MetersPerDegree;
        var east = (towardLongitude - longitude) * metersPerLongitude;
        var distance = Math.Sqrt(north * north + east * east);
        if (distance < 1)
        {
            return (latitude, longitude);
        }

        var (sin, cos) = Math.SinCos(double.DegreesToRadians(degrees));
        var turnedNorth = (north * cos - east * sin) / distance * meters;
        var turnedEast = (east * cos + north * sin) / distance * meters;
        return (Math.Round(latitude + turnedNorth / MetersPerDegree, 5), Math.Round(longitude + turnedEast / metersPerLongitude, 5));
    }
}
