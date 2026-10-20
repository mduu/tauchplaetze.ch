using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Forecasts.Calculation;

namespace Tauchplaetze.Core.Tests.Features.Forecasts;

public class DischargeRulesTests
{
    [Theory]
    [InlineData(49.9, DischargeLevel.VeryLow)]
    [InlineData(50, DischargeLevel.Low)]
    [InlineData(99.9, DischargeLevel.Low)]
    [InlineData(100, DischargeLevel.Medium)]
    [InlineData(199.9, DischargeLevel.Medium)]
    [InlineData(200, DischargeLevel.Strong)]
    public void Ohne_Schwellen_des_Platzes_ordnet_der_Median_der_letzten_30_Tage_den_Abfluss_ein(decimal discharge, DischargeLevel expected)
    {
        // Arrange
        var thresholds = DischargeRules.ThresholdsFor(null, median: 100)!;

        // Act
        var level = DischargeRules.LevelOf(discharge, thresholds);

        // Assert
        Assert.Equal(expected, level);
    }

    [Fact]
    public void Ein_Median_unter_1_m3_pro_s_zaehlt_als_1_m3_pro_s()
    {
        // Arrange: ein Bach mit fast keinem Wasser.
        var thresholds = DischargeRules.ThresholdsFor(null, median: 0.01m)!;

        // Act
        var level = DischargeRules.LevelOf(0.3m, thresholds);

        // Assert
        Assert.Equal(DischargeLevel.VeryLow, level);
    }

    [Fact]
    public void Die_Schwellen_des_Platzes_gelten_vor_dem_Median()
    {
        // Arrange
        var thresholds = DischargeRules.ThresholdsFor(new DischargeThresholds(300, 500, 800), median: 100)!;

        // Act
        var level = DischargeRules.LevelOf(450, thresholds);

        // Assert
        Assert.Equal(DischargeLevel.Low, level);
    }

    [Fact]
    public void Ohne_Median_und_ohne_Schwellen_bleibt_der_Abfluss_uneingeordnet()
    {
        // Act
        var thresholds = DischargeRules.ThresholdsFor(null, median: null);

        // Assert
        Assert.Null(thresholds);
    }

    [Theory]
    [InlineData(105, DischargeTrend.Steady)]
    [InlineData(95, DischargeTrend.Steady)]
    [InlineData(105.1, DischargeTrend.Rising)]
    [InlineData(94.9, DischargeTrend.Falling)]
    public void Innerhalb_von_5_Prozent_des_Werts_von_vor_3_Stunden_bleibt_der_Abfluss_gleich(decimal now, DischargeTrend expected)
    {
        // Act
        var trend = DischargeRules.TrendOf(now, before: 100);

        // Assert
        Assert.Equal(expected, trend);
    }
}
