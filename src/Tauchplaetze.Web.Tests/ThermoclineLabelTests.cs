using Tauchplaetze.Web.Features.Forecasts;

namespace Tauchplaetze.Web.Tests;

public class ThermoclineLabelTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.5, 2)]
    [InlineData(1, 12)]
    public void Eine_Beschriftung_der_Sprungschicht_ueberdeckt_nie_die_Wasseroberflaeche(double top, double bottom)
    {
        // Arrange
        static double Y(decimal depth) => 34 + (double)depth * 6.6;

        // Act
        var labels = ThermoclineLabels.For((decimal)top, (decimal)bottom, Y, 26, minY: 52);

        // Assert
        Assert.All(labels, label => Assert.True(label.Y >= 52));
    }
}
