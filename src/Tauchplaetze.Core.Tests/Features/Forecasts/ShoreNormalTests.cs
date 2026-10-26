using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.Features.Forecasts.Calculation;
using Tauchplaetze.Core.Features.Forecasts.Sources.Bathymetry;

namespace Tauchplaetze.Core.Tests.Features.Forecasts;

public class ShoreNormalTests
{
    private const double Latitude = 47.29;
    private const double Longitude = 8.6;

    [Fact]
    public void Die_Richtung_in_den_See_zeigt_rechtwinklig_auf_die_naechste_Tiefenlinie()
    {
        // Arrange: eine Linie in Nord-Süd-Richtung 100 m westlich, eine zweite 300 m südlich.
        IReadOnlyList<(double, double)> west = [(Latitude + 0.005, Longitude - Degrees(100)), (Latitude - 0.005, Longitude - Degrees(100))];
        IReadOnlyList<(double, double)> south = [(Latitude - 300 / 111_320.0, Longitude - 0.01), (Latitude - 300 / 111_320.0, Longitude + 0.01)];

        // Act
        var target = ShoreNormal.Toward(Latitude, Longitude, [south, west]);

        // Assert
        Assert.NotNull(target);
        Assert.InRange(Bearing(Latitude, Longitude, target.Value), 269.5, 270.5);
    }

    [Fact]
    public void Ohne_Tiefenlinie_im_Umkreis_von_1_km_gibt_es_keine_Richtung()
    {
        // Arrange
        IReadOnlyList<(double, double)> far = [(Latitude + 0.005, Longitude - Degrees(1100)), (Latitude - 0.005, Longitude - Degrees(1100))];

        // Act
        var target = ShoreNormal.Toward(Latitude, Longitude, [far]);

        // Assert
        Assert.Null(target);
    }

    [Theory]
    [InlineData(47.291418, 8.600592)]
    [InlineData(47.293211, 8.598912)]
    [InlineData(47.294031, 8.598121)]
    public void Am_Zuerichsee_bei_Herrliberg_zeigt_die_Richtung_nach_Suedwesten_statt_dem_Ufer_entlang(double latitude, double longitude)
    {
        // Arrange: Steinrad, Bocchiaplatz, Kleiner Parkplatz. Das Ufer verläuft nach SSE, der Satellitenpunkt des Sees liegt im Süden.
        var contours = new ContourTiles(Options.Create(new BathymetryOptions { Directory = BathymetryDirectory() }), NullLogger<ContourTiles>.Instance);

        // Act
        var target = ShoreNormal.Toward(latitude, longitude, contours.Near("zuerichsee", latitude, longitude, ShoreNormal.ContourDepth, ShoreNormal.MaxMeters));

        // Assert
        Assert.NotNull(target);
        Assert.InRange(Bearing(latitude, longitude, target.Value), 220, 240);
    }

    private static double Degrees(double meters) => meters / (111_320 * Math.Cos(double.DegreesToRadians(Latitude)));

    private static double Bearing(double latitude, double longitude, (double Latitude, double Longitude) target)
    {
        var north = target.Latitude - latitude;
        var east = (target.Longitude - longitude) * Math.Cos(double.DegreesToRadians(latitude));
        return (double.RadiansToDegrees(Math.Atan2(east, north)) + 360) % 360;
    }

    private static string BathymetryDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tauchplaetze.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("Tauchplaetze.slnx"), "Tauchplaetze.Web", "wwwroot", "bathymetry");
    }
}
