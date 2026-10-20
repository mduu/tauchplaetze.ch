using Tauchplaetze.Core.Features.Forecasts.Calculation;

namespace Tauchplaetze.Core.Tests.Features.Forecasts;

public class InflowWeightsTests
{
    [Theory]
    [InlineData(2.0, 1.0)]
    [InlineData(5.0, 1.0)]
    [InlineData(10.0, 0.5)]
    [InlineData(15.0, 0.0)]
    [InlineData(40.0, 0.0)]
    public void Ein_Zufluss_zaehlt_bis_5_km_von_der_Muendung_voll_und_ab_15_km_nicht_mehr(double km, double expected)
    {
        // Act
        var weight = InflowWeights.Weight(km);

        // Assert
        Assert.Equal(expected, weight, 3);
    }

    [Theory]
    [InlineData(3.0, 2.0, 3.0)]
    [InlineData(3.0, 10.0, 2.0)]
    [InlineData(3.0, 20.0, null)]
    public void Der_Quotient_am_Platz_naehert_sich_mit_der_Distanz_dem_ueblichen_Abfluss(double ratio, double km, double? expected)
    {
        // Act
        var effective = InflowWeights.Effective([new WeightedInflow(ratio, InflowWeights.Weight(km))]);

        // Assert
        Assert.Equal(expected, effective);
    }

    [Fact]
    public void Bei_mehreren_Zufluessen_zaehlt_der_hoechste_Quotient_am_Platz()
    {
        // Arrange: ein starkes Hochwasser weit weg und ein mässiges nahe.
        WeightedInflow[] inflows = [new(5.0, InflowWeights.Weight(12)), new(2.0, InflowWeights.Weight(1))];

        // Act
        var effective = InflowWeights.Effective(inflows);

        // Assert
        Assert.Equal(2.2, effective);
    }

    [Fact]
    public void Ohne_bekannte_Muendung_zaehlt_der_Zufluss_voll()
    {
        // Act
        var weight = InflowWeights.Weight(null);

        // Assert
        Assert.Equal(1.0, weight);
    }
}
