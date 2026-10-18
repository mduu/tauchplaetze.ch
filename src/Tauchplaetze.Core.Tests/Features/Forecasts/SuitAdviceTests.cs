using Tauchplaetze.Core.Features.Forecasts;

namespace Tauchplaetze.Core.Tests.Features.Forecasts;

public class SuitAdviceTests
{
    [Theory]
    [InlineData(13.9, 18.0, SuitAdvice.DrySuit)]
    [InlineData(14.0, 22.0, SuitAdvice.WetSuit7)]
    [InlineData(20.0, 24.0, SuitAdvice.WetSuit5)]
    [InlineData(null, 16.5, SuitAdvice.WetSuit7)]
    public void Der_Anzug_folgt_der_Temperatur_auf_20_m_sonst_der_Oberflaeche(double? at20m, double surface, SuitAdvice expected)
    {
        // Arrange
        var day = new ForecastDay(new DateOnly(2026, 10, 18), 3, 8, ProfileSource.Model3d, (decimal)surface, null, (decimal?)at20m, null, null, null,
            null, null, VisibilityIndicator.Unknown, null, 2, 8, 12, 0, AlgaeLevel.Low, []);

        // Act
        var suit = day.Suit;

        // Assert
        Assert.Equal(expected, suit);
    }
}
