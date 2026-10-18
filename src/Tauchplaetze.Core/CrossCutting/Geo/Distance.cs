using NetTopologySuite.Geometries;

namespace Tauchplaetze.Core.CrossCutting.Geo;

internal static class Distance
{
    private const double EarthRadiusKm = 6371;

    /// <summary>Distanz zweier WGS84-Punkte auf der Kugel (Haversine).</summary>
    public static double Km(Point from, Point to)
    {
        var dLat = Radians(to.Y - from.Y);
        var dLng = Radians(to.X - from.X);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(Radians(from.Y)) * Math.Cos(Radians(to.Y)) * Math.Pow(Math.Sin(dLng / 2), 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Sqrt(a));
    }

    private static double Radians(double degrees) => degrees * Math.PI / 180;
}
