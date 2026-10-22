using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Forecasts.Calculation;
using Tauchplaetze.Core.Features.Forecasts.Sources.Alplakes;

namespace Tauchplaetze.Core.Tests.Features.Forecasts;

public class ModelPointTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(1.5, true)]
    [InlineData(29, true)]
    [InlineData(30, false)]
    [InlineData(79, false)]
    public void Ein_Profil_das_nicht_bis_30_m_reicht_braucht_einen_Punkt_weiter_draussen(double deepest, bool expected)
    {
        // Act
        var tooShallow = ModelPoint.TooShallow(Reaching(deepest));

        // Assert
        Assert.Equal(expected, tooShallow);
    }

    [Fact]
    public void Ohne_Antwort_am_Platz_gibt_es_keinen_Punkt_weiter_draussen()
    {
        // Act
        var tooShallow = ModelPoint.TooShallow(null);

        // Assert
        Assert.False(tooShallow);
    }

    [Fact]
    public void Ein_Gitterpunkt_ohne_Werte_in_der_Tiefe_zaehlt_als_flach()
    {
        // Arrange: Unter dem Seegrund liefert das Modell keine Werte.
        var series = new DepthTimeSeries([Noon], [0.1, 1.5, 40], new double?[,] { { 15.3 }, { 15.1 }, { null } }, 25);

        // Act
        var tooShallow = ModelPoint.TooShallow(series);

        // Assert
        Assert.True(tooShallow);
    }

    [Fact]
    public void Vom_Platz_und_dem_Punkt_draussen_gilt_das_tiefere_Profil_bei_gleicher_Tiefe_das_naehere()
    {
        // Arrange
        var atSite = Reaching(1.5);
        var seaward = Reaching(79);
        var equallyDeep = Reaching(1.5);

        // Act
        var deeper = ModelPoint.Deeper(atSite, seaward);
        var tie = ModelPoint.Deeper(atSite, equallyDeep);

        // Assert
        Assert.Same(seaward, deeper);
        Assert.Same(atSite, tie);
    }

    [Fact]
    public void Die_Distanz_zum_Gitterpunkt_zaehlt_ab_dem_Platz_auch_fuer_den_Punkt_draussen()
    {
        // Arrange: Gitterpunkt 300 m vom Punkt 250 m draussen, also bis 550 m vom Platz.
        var day = new DateOnly(2026, 10, 22);
        var seaward = ModelPoint.FromSite(Reaching(79, distance: 300), 250);
        var lakeWide = Reaching(79, distance: null);

        // Act
        var profile = DataLevelRule.ChooseProfile(day, seaward, lakeWide);

        // Assert
        Assert.Equal(ProfileSource.Model1d, profile!.Source);
    }

    private static DepthTimeSeries Reaching(double deepest, double? distance = 25) =>
        new([Noon], [0.1, deepest], new double?[,] { { 15.3 }, { 8.2 } }, distance);
}
