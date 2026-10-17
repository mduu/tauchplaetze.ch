using NetTopologySuite.Geometries;

namespace Tauchplaetze.Core.CrossCutting.Geo;

/// <summary>Punkte in WGS84 (SRID 4326). NetTopologySuite erwartet X = Länge, Y = Breite.</summary>
internal static class GeoPoints
{
    public const int Wgs84 = 4326;

    public static Point Create(double latitude, double longitude) => new(longitude, latitude) { SRID = Wgs84 };
}
