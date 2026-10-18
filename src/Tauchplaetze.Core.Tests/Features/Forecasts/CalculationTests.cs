using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Forecasts.Calculation;
using Tauchplaetze.Core.Features.Forecasts.Sources.Alplakes;
using Tauchplaetze.Core.Features.Forecasts.Sources.OpenMeteo;

namespace Tauchplaetze.Core.Tests.Features.Forecasts;

public class ThermoclineTests
{
    [Fact]
    public void Im_Herbst_liegt_die_Sprungschicht_im_Zuerichsee_zwischen_13_und_27_m()
    {
        // Arrange
        var series = DepthTimeSeries.Parse(TestData.Read("alplakes", "depthtime-delft3d-flow-zurich.json"), "temperature");
        var profile = series.ProfileAt(new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1))!;

        // Act
        var thermocline = ThermoclineDetector.Detect(profile, 0.5);

        // Assert
        Assert.Equal(12.8, thermocline!.Top, 0);
        Assert.Equal(27, thermocline.Bottom, 0);
    }

    [Fact]
    public void Ein_durchmischter_See_hat_keine_Sprungschicht()
    {
        // Arrange
        var winter = Profiles.Of((0, 5.8), (10, 5.7), (20, 5.6), (30, 5.5), (40, 5.4));

        // Act
        var thermocline = ThermoclineDetector.Detect(winter, 0.5);

        // Assert
        Assert.Null(thermocline);
    }

    [Theory]
    [InlineData(0.5, 0)]
    [InlineData(5.4, 1)]
    [InlineData(11.5, 3)]
    [InlineData(28, 4)]
    [InlineData(130, 12)]
    public void Wind_in_km_h_ergibt_Beaufort_nach_WMO(double kmh, int expected)
    {
        // Act
        var beaufort = Beaufort.FromKmh(kmh);

        // Assert
        Assert.Equal(expected, beaufort);
    }
}

public class DayWeatherTests
{
    private static readonly DateOnly Day = new(2026, 10, 18);

    [Fact]
    public void Der_Wind_kommt_aus_dem_ersten_Modell_das_das_Tauchfenster_ganz_abdeckt()
    {
        // Arrange
        var fine = new ModelWeather("ch1", Hours(Day, wind: 5, until: 12));
        var coarse = new ModelWeather("ch2", Hours(Day, wind: 18));

        // Act
        var weather = DayWeather.For(Day, [fine, coarse], DiveWindow.Default);

        // Assert
        Assert.Equal(("ch2", 18.0, 3), (weather.Model, weather.WindMaxKmh!.Value, weather.WindBeaufort!.Value));
    }

    [Fact]
    public void Regen_72_h_zaehlt_die_drei_Tage_vor_dem_Tag_ohne_den_Tag_selbst()
    {
        // Arrange
        var hours = Enumerable.Range(-72, 96)
            .Select(h => new WeatherHour(Day.ToDateTime(TimeOnly.MinValue).AddHours(h), 3, 180, 6, h < 0 ? 0.5 : 10))
            .ToList();

        // Act
        var weather = DayWeather.For(Day, [new ModelWeather("ch2", hours)], DiveWindow.Default);

        // Assert
        Assert.Equal(36, weather.PrecipitationLast72h);
        Assert.Equal(80, weather.PrecipitationInDiveWindow);
    }

    [Fact]
    public void Ohne_Modell_fuer_das_ganze_Tauchfenster_bleibt_der_Wind_leer()
    {
        // Act
        var weather = DayWeather.For(Day, [new ModelWeather("ch1", Hours(Day, wind: 5, until: 12))], DiveWindow.Default);

        // Assert
        Assert.Null(weather.WindMaxKmh);
        Assert.Null(weather.WindBeaufort);
    }

    private static List<WeatherHour> Hours(DateOnly day, double wind, int until = 24) =>
        Enumerable.Range(0, until).Select(h => new WeatherHour(day.ToDateTime(new TimeOnly(h, 0)), wind, 270, wind * 1.5, 0)).ToList();
}

public class ClarityTests
{
    private static readonly DateOnly Day = new(2026, 10, 18);

    [Fact]
    public void Ein_Satellitenwert_aelter_als_14_Tage_macht_die_Klarheit_unbekannt_mit_Datum_des_letzten_Bilds()
    {
        // Act
        var clarity = ClarityEstimate.For(Day, new SatelliteObservation(new DateOnly(2026, 10, 3), 6.3), 0, 1, DiveIndexRules.Default);

        // Assert
        Assert.Equal(new ClarityEstimate(null, new DateOnly(2026, 10, 3), VisibilityIndicator.Unknown), clarity);
    }

    [Theory]
    [InlineData(0, 1.0, 8.0, VisibilityIndicator.Good)]
    [InlineData(30, 1.0, 4.8, VisibilityIndicator.Moderate)]
    [InlineData(0, 3.0, 4.8, VisibilityIndicator.Moderate)]
    [InlineData(17.5, 2.1, 6.4, VisibilityIndicator.Good)]
    public void Regen_und_Abfluss_seit_dem_Bild_mindern_die_Klarheit(double rain, double dischargeRatio, double expectedDepth, VisibilityIndicator expected)
    {
        // Act
        var clarity = ClarityEstimate.For(Day, new SatelliteObservation(new DateOnly(2026, 10, 12), 8), rain, dischargeRatio, DiveIndexRules.Default);

        // Assert
        Assert.Equal((expectedDepth, expected), (clarity.SecchiDepth!.Value, clarity.Indicator));
    }

    [Theory]
    [InlineData(2.9, AlgaeLevel.Low)]
    [InlineData(6, AlgaeLevel.Medium)]
    [InlineData(25, AlgaeLevel.High)]
    public void Chlorophyll_ergibt_die_Algenstufe(double chlorophyll, AlgaeLevel expected)
    {
        // Act
        var algae = ClarityEstimate.AlgaeFor(Day, new SatelliteObservation(new DateOnly(2026, 10, 15), chlorophyll), DiveIndexRules.Default);

        // Assert
        Assert.Equal(expected, algae);
    }
}

public class DataLevelTests
{
    [Theory]
    [InlineData(false, ProfileSource.Model3d, 3)]
    [InlineData(false, ProfileSource.Model1d, 2)]
    [InlineData(false, ProfileSource.RiverStation, 2)]
    [InlineData(false, null, 1)]
    [InlineData(true, ProfileSource.Model3d, 0)]
    public void Die_Datenstufe_folgt_der_Herkunft_des_Profils(bool forecastDisabled, ProfileSource? source, byte expected)
    {
        // Act
        var level = DataLevelRule.For(forecastDisabled, source);

        // Assert
        Assert.Equal(expected, level);
    }

    [Fact]
    public void Ist_der_Gitterpunkt_des_3D_Modells_zu_weit_weg_gilt_das_1D_Profil_des_Sees()
    {
        // Arrange
        var day = new DateOnly(2026, 10, 18);
        var noon = new DateTimeOffset(2026, 10, 18, 10, 0, 0, TimeSpan.Zero);
        var far3d = Profiles.Series(noon, distance: 800);
        var lakeWide = Profiles.Series(noon, distance: null);

        // Act
        var profile = DataLevelRule.ChooseProfile(day, far3d, lakeWide);

        // Assert
        Assert.Equal(ProfileSource.Model1d, profile!.Source);
    }
}

public class DiveIndexTests
{
    private static readonly DayWeather Calm = new("ch2", 8, 12, 200, 0, 0);

    private static readonly DayProfile Summer = new(Profiles.Of((0, 20), (8, 19.5), (12, 14), (16, 9), (25, 6.5), (40, 5.5)), ProfileSource.Model3d);

    [Fact]
    public void Ruhiger_klarer_See_mit_hoher_Sprungschicht_ergibt_10()
    {
        // Arrange
        var inputs = new DayInputs(Calm, Summer, Clear(12), AlgaeLevel.Low, 1.0, IsRiver: false);

        // Act
        var result = DiveIndexCalculator.Calculate(inputs, DiveIndexRules.Default);

        // Assert
        Assert.Equal(10, result.DiveIndex);
        Assert.Equal([ReasonCode.ClearBelowThermocline, ReasonCode.ClearWater, ReasonCode.CalmLake], result.Reasons);
        Assert.Equal(16, result.VisibilityGoodBelow);
    }

    [Fact]
    public void Ohne_Wind_gibt_es_keinen_Index()
    {
        // Arrange
        var inputs = new DayInputs(Calm with { WindMaxKmh = null }, Summer, Clear(8), AlgaeLevel.Low, null, IsRiver: false);

        // Act
        var result = DiveIndexCalculator.Calculate(inputs, DiveIndexRules.Default);

        // Assert
        Assert.Null(result.DiveIndex);
    }

    [Fact]
    public void Ohne_Wasserdaten_ist_der_Tag_ein_Ausblick_ohne_Index()
    {
        // Arrange
        var inputs = new DayInputs(Calm, null, ClarityEstimate.Unknown, AlgaeLevel.Unknown, null, IsRiver: false);

        // Act
        var result = DiveIndexCalculator.Calculate(inputs, DiveIndexRules.Default);

        // Assert
        Assert.Null(result.DiveIndex);
        Assert.Contains(ReasonCode.ClarityUnknown, result.Reasons);
    }

    [Fact]
    public void Fehlt_die_Klarheit_verteilt_sich_ihr_Gewicht_auf_die_anderen_Teilwerte()
    {
        // Arrange
        var windy = Calm with { WindMaxKmh = 24 };
        var inputs = new DayInputs(windy, Summer, ClarityEstimate.Unknown, AlgaeLevel.Unknown, null, IsRiver: false);

        // Act
        var result = DiveIndexCalculator.Calculate(inputs, DiveIndexRules.Default);

        // Assert
        Assert.Null(result.Scores.Clarity);
        Assert.Equal(Math.Round((4 * 0.25 + 10 * 0.20 + 10 * 0.20) / 0.65, 1), result.DiveIndex);
    }

    [Fact]
    public void Nach_Starkregen_mit_hohem_Abfluss_und_tiefer_Sprungschicht_sinkt_der_Index_deutlich()
    {
        // Arrange
        var stormy = Calm with { WindMaxKmh = 30, PrecipitationLast72h = 45 };
        var deep = new DayProfile(Profiles.Of((0, 14), (20, 13.5), (26, 10), (32, 6.5), (40, 6)), ProfileSource.Model3d);
        var inputs = new DayInputs(stormy, deep, Clear(2.5), AlgaeLevel.Low, 3.2, IsRiver: false);

        // Act
        var result = DiveIndexCalculator.Calculate(inputs, DiveIndexRules.Default);

        // Assert
        Assert.True(result.DiveIndex < 3, $"Index {result.DiveIndex}");
        Assert.Equal([ReasonCode.TurbidAfterRain, ReasonCode.Windy, ReasonCode.TurbidWater], result.Reasons);
    }

    [Fact]
    public void Bei_einem_Fluss_entfaellt_die_Schichtung()
    {
        // Arrange
        var river = new DayProfile(Profiles.Of((0, 12.5)), ProfileSource.RiverStation);
        var inputs = new DayInputs(Calm, river, ClarityEstimate.Unknown, AlgaeLevel.Unknown, 1.0, IsRiver: true);

        // Act
        var result = DiveIndexCalculator.Calculate(inputs, DiveIndexRules.Default);

        // Assert
        Assert.Null(result.Scores.Stratification);
        Assert.Equal(10, result.DiveIndex);
        Assert.DoesNotContain(ReasonCode.ClarityUnknown, result.Reasons);
    }

    private static ClarityEstimate Clear(double secchi) =>
        ClarityEstimate.For(new DateOnly(2026, 10, 18), new SatelliteObservation(new DateOnly(2026, 10, 15), secchi), 0, 1, DiveIndexRules.Default);
}

internal static class Profiles
{
    public static TemperatureProfile Of(params (double Depth, double Temperature)[] points) =>
        new(DateTimeOffset.UnixEpoch, points.Select(p => new ProfilePoint(p.Depth, p.Temperature)).ToList());

    public static DepthTimeSeries Series(DateTimeOffset time, double? distance)
    {
        var temperatures = new double?[,] { { 15 }, { 10 } };
        return new DepthTimeSeries([time], [0, 20], temperatures, distance);
    }
}
