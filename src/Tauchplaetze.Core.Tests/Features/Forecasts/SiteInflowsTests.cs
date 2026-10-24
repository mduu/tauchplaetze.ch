using Tauchplaetze.Core.Features.Forecasts;

namespace Tauchplaetze.Core.Tests.Features.Forecasts;

public class SiteInflowsTests
{
    private static readonly DateOnly Today = new(2026, 10, 24);

    [Theory]
    [InlineData(0, true, true, 1.5, true)]
    [InlineData(0, false, true, 2.5, true)]
    [InlineData(2, true, true, 2.5, true)]
    [InlineData(2, true, false, 1.5, false)]
    [InlineData(2, false, false, null, false)]
    public void Heute_zaehlt_der_frische_Messwert_sonst_die_Vorhersage_und_kuenftig_die_Vorhersage(
        int daysAhead, bool hasMeasured, bool hasForecast, double? expected, bool isCurrent)
    {
        // Arrange: ein veralteter Messwert fehlt in «measured» (`rules-1.7`).
        var day = Today.AddDays(daysAhead);
        var measured = hasMeasured ? new Dictionary<string, double> { ["2091"] = 1.5 } : [];
        var forecast = hasForecast ? new Dictionary<(string, DateOnly), double> { [("2091", day)] = 2.5 } : [];

        // Act
        var ratio = SiteInflows.Ratio([new SiteInflow("2091", 1)], day, Today, measured, forecast);

        // Assert
        Assert.Equal(expected, ratio.Value);
        Assert.Equal(isCurrent, ratio.IsCurrent);
    }
}
