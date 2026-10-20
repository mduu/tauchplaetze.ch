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
    [InlineData(0, 1.0, 8.0, 5.6)]
    [InlineData(30, 1.0, 4.8, 3.4)]
    [InlineData(0, 3.0, 4.8, 3.4)]
    [InlineData(17.5, 2.1, 6.4, 4.5)]
    public void Regen_und_Abfluss_seit_dem_Bild_mindern_die_Klarheit_und_in_Ufernaehe_gilt_nur_ein_Teil(
        double rain, double dischargeRatio, double expectedOpenWater, double expectedShore)
    {
        // Act
        var clarity = ClarityEstimate.For(Day, new SatelliteObservation(new DateOnly(2026, 10, 12), 8), rain, dischargeRatio, DiveIndexRules.Default);

        // Assert
        Assert.Equal((expectedOpenWater, expectedShore), (clarity.SecchiDepth!.Value, clarity.ShoreDepth!.Value));
    }

    [Theory]
    [InlineData(10, VisibilityIndicator.Good)]
    [InlineData(8, VisibilityIndicator.Moderate)]
    [InlineData(4, VisibilityIndicator.Poor)]
    public void Der_Sicht_Indikator_gilt_fuer_die_Ufernaehe(double openWater, VisibilityIndicator expected)
    {
        // Act
        var clarity = ClarityEstimate.For(Day, new SatelliteObservation(new DateOnly(2026, 10, 16), openWater), 0, 1, DiveIndexRules.Default);

        // Assert
        Assert.Equal(expected, clarity.Indicator);
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
        var inputs = new DayInputs(Calm, Summer, Clear(15), AlgaeLevel.Low, 1.0, IsRiver: false);

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
    public void Fehlt_die_Klarheit_zaehlt_sie_vorsichtig_die_Schichtung_hoechstens_7_und_der_Index_hoechstens_6_5()
    {
        // Arrange
        var inputs = new DayInputs(Calm, Summer, ClarityEstimate.Unknown, AlgaeLevel.Unknown, null, IsRiver: false);

        // Act
        var result = DiveIndexCalculator.Calculate(inputs, DiveIndexRules.Default);

        // Assert
        Assert.Equal((4.0, 7.0), (result.Scores.Clarity, result.Scores.Stratification));
        Assert.Equal(6.5, result.DiveIndex);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(33)]
    public void Ohne_Sichtwert_ist_der_Index_nie_besser_als_mit_frischen_4_7_m_in_Ufernaehe(double windKmh)
    {
        // Arrange
        var weather = Calm with { WindMaxKmh = windKmh };
        var unknown = new DayInputs(weather, Summer, ClarityEstimate.Unknown, AlgaeLevel.Unknown, 1.0, IsRiver: false);
        var moderate = new DayInputs(weather, Summer, Clear(6.72), AlgaeLevel.Low, 1.0, IsRiver: false);

        // Act
        var withoutClarity = DiveIndexCalculator.Calculate(unknown, DiveIndexRules.Default).DiveIndex!.Value;
        var withModerate = DiveIndexCalculator.Calculate(moderate, DiveIndexRules.Default).DiveIndex!.Value;

        // Assert
        Assert.Equal(4.7, moderate.Clarity.ShoreDepth);
        Assert.True(withoutClarity <= withModerate, $"ohne Sicht {withoutClarity}, 4.7 m {withModerate}");
    }

    [Theory]
    [InlineData(8.58, 6.0, 10.0, true)]
    [InlineData(8.43, 5.9, 7.0, true)]
    [InlineData(4.29, 3.0, 7.0, true)]
    [InlineData(4.14, 2.9, 7.0, false)]
    public void Erst_ab_6_m_in_Ufernaehe_gibt_die_Schichtung_volle_Punkte_unten_klarer_gilt_ab_3_m(double openWater, double shore, double stratification, bool clearBelow)
    {
        // Arrange
        var inputs = new DayInputs(Calm, Summer, Clear(openWater), AlgaeLevel.Low, 1.0, IsRiver: false);

        // Act
        var result = DiveIndexCalculator.Calculate(inputs, DiveIndexRules.Default);

        // Assert
        Assert.Equal(shore, inputs.Clarity.ShoreDepth);
        Assert.Equal(stratification, result.Scores.Stratification);
        Assert.Equal(clearBelow, result.Reasons.Contains(ReasonCode.ClearBelowThermocline));
    }

    [Theory]
    [InlineData(5, false)]
    [InlineData(6, true)]
    [InlineData(14, true)]
    [InlineData(15, true)]
    public void Sobald_der_Satellitenwert_altert_gilt_die_Obergrenze_ohne_Sichtwert(int ageDays, bool capped)
    {
        // Arrange
        var result = IndexAtAge(ageDays);

        // Assert
        Assert.Equal(capped, result.DiveIndex <= 6.5);
        Assert.Equal(capped, result.Scores.Stratification <= 7);
    }

    [Fact]
    public void Am_Tag_an_dem_der_Satellitenwert_unbekannt_wird_springt_der_Index_nicht_nach_oben()
    {
        // Act
        var lastDay = IndexAtAge(14).DiveIndex!.Value;
        var unknown = IndexAtAge(15).DiveIndex!.Value;

        // Assert
        Assert.True(unknown <= lastDay, $"Tag 14: {lastDay}, Tag 15: {unknown}");
    }

    [Theory]
    [InlineData(5, 10.0)]
    [InlineData(10, 6.7)]
    [InlineData(14, 4.0)]
    public void Ein_alter_Satellitenwert_naehert_sich_bis_Tag_14_dem_vorsichtigen_Wert(int ageDays, double expected)
    {
        // Arrange
        var day = new DateOnly(2026, 10, 18);
        var clarity = ClarityEstimate.For(day, new SatelliteObservation(day.AddDays(-ageDays), 15), 0, 1, DiveIndexRules.Default);
        var inputs = new DayInputs(Calm, Summer, clarity, AlgaeLevel.Low, 1.0, IsRiver: false);

        // Act
        var result = DiveIndexCalculator.Calculate(inputs, DiveIndexRules.Default);

        // Assert
        Assert.Equal(expected, Math.Round(result.Scores.Clarity!.Value, 1));
        Assert.Equal(ageDays > 5, result.Reasons.Contains(ReasonCode.ClarityStale));
    }

    [Fact]
    public void Ein_schlechter_Satellitenwert_wird_durch_das_Alter_nicht_besser()
    {
        // Arrange
        var day = new DateOnly(2026, 10, 18);
        var clarity = ClarityEstimate.For(day, new SatelliteObservation(day.AddDays(-12), 3), 0, 1, DiveIndexRules.Default);
        var inputs = new DayInputs(Calm, Summer, clarity, AlgaeLevel.Low, 1.0, IsRiver: false);

        // Act
        var result = DiveIndexCalculator.Calculate(inputs, DiveIndexRules.Default);

        // Assert
        Assert.True(result.Scores.Clarity < 4, $"Klarheit {result.Scores.Clarity}");
    }

    [Fact]
    public void Ist_die_Oberflaeche_in_Ufernaehe_trueb_gibt_die_Schichtung_hoechstens_7_Punkte()
    {
        // Arrange
        var inputs = new DayInputs(Calm, Summer, Clear(4), AlgaeLevel.Low, 1.0, IsRiver: false);

        // Act
        var result = DiveIndexCalculator.Calculate(inputs, DiveIndexRules.Default);

        // Assert
        Assert.Equal(7, result.Scores.Stratification);
        Assert.DoesNotContain(ReasonCode.ClearBelowThermocline, result.Reasons);
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
    public void Bei_einem_Fluss_entfaellt_die_Schichtung_und_ohne_Sichtwert_gilt_hoechstens_6_5()
    {
        // Arrange
        var river = new DayProfile(Profiles.Of((0, 12.5)), ProfileSource.RiverStation);
        var inputs = new DayInputs(Calm, river, ClarityEstimate.Unknown, AlgaeLevel.Unknown, 1.0, IsRiver: true);

        // Act
        var result = DiveIndexCalculator.Calculate(inputs, DiveIndexRules.Default);

        // Assert
        Assert.Null(result.Scores.Stratification);
        Assert.Equal(6.5, result.DiveIndex);
        Assert.DoesNotContain(ReasonCode.ClarityUnknown, result.Reasons);
    }

    private static DayResult IndexAtAge(int ageDays)
    {
        var day = new DateOnly(2026, 10, 18);
        var clarity = ClarityEstimate.For(day, new SatelliteObservation(day.AddDays(-ageDays), 15), 0, 1, DiveIndexRules.Default);
        return DiveIndexCalculator.Calculate(new DayInputs(Calm, Summer, clarity, AlgaeLevel.Low, 1.0, IsRiver: false), DiveIndexRules.Default);
    }

    [Theory]
    [InlineData(2.9, 5.0, true)]
    [InlineData(3.0, 6.6, false)]
    [InlineData(5.9, 7.0, true)]
    [InlineData(6.0, 8.4, false)]
    public void Maessige_Sicht_in_Ufernaehe_begrenzt_den_Index_auf_7_schlechte_auf_5(double shore, double expected, bool limited)
    {
        // Arrange
        var perfectDay = new DayInputs(Calm, Summer, Shore(shore), AlgaeLevel.Low, 1.0, IsRiver: false);

        // Act
        var result = DiveIndexCalculator.Calculate(perfectDay, DiveIndexRules.Default);

        // Assert
        Assert.Equal(expected, result.DiveIndex!.Value, 1);
        Assert.Equal(limited, result.Reasons.Contains(ReasonCode.VisibilityLimitsIndex));
    }

    [Fact]
    public void Ohne_Sichtwert_bleibt_die_Obergrenze_6_5_ohne_Begruendung_der_Sicht()
    {
        // Arrange
        var inputs = new DayInputs(Calm, Summer, ClarityEstimate.Unknown, AlgaeLevel.Unknown, 1.0, IsRiver: false);

        // Act
        var result = DiveIndexCalculator.Calculate(inputs, DiveIndexRules.Default);

        // Assert
        Assert.Equal(6.5, result.DiveIndex);
        Assert.DoesNotContain(ReasonCode.VisibilityLimitsIndex, result.Reasons);
    }

    [Fact]
    public void Liegt_der_Index_schon_unter_der_Grenze_nennt_die_Begruendung_die_Sicht_nicht()
    {
        // Arrange
        var stormy = new DayInputs(Calm with { WindMaxKmh = 40 }, Summer, Shore(4), AlgaeLevel.Low, 1.0, IsRiver: false);

        // Act
        var result = DiveIndexCalculator.Calculate(stormy, DiveIndexRules.Default);

        // Assert
        Assert.True(result.DiveIndex < 7, $"Index {result.DiveIndex}");
        Assert.DoesNotContain(ReasonCode.VisibilityLimitsIndex, result.Reasons);
    }

    [Theory]
    [InlineData(1.4, 0)]
    [InlineData(2.25, 1.5)]
    [InlineData(3.0, 3)]
    [InlineData(5.0, 3)]
    public void Am_Fluss_zieht_viel_Wasser_ab_1_5_mal_dem_Median_bis_3_Punkte_fuer_die_Stroemung_ab(double ratio, double expectedPenalty)
    {
        // Arrange
        var river = new DayProfile(Profiles.Of((0, 12)), ProfileSource.RiverStation);
        var calm = new DayInputs(Calm, river, ClarityEstimate.Unknown, AlgaeLevel.Unknown, 1.0, IsRiver: true);
        var high = calm with { DischargeRatio = ratio };
        var withoutPenalty = DiveIndexCalculator.Calculate(high, DiveIndexRules.Default with { RiverCurrentPenalty = [new(100, 0), new(101, 0)] });

        // Act
        var result = DiveIndexCalculator.Calculate(high, DiveIndexRules.Default);

        // Assert
        Assert.Equal(Math.Max(withoutPenalty.DiveIndex!.Value - expectedPenalty, 1), result.DiveIndex!.Value, 1);
        Assert.Equal(expectedPenalty > 0, result.Reasons.Contains(ReasonCode.StrongCurrent));
    }

    [Fact]
    public void Der_Abzug_fuer_die_Stroemung_hebt_einen_Index_unter_1_nie_an()
    {
        // Arrange: ein Index von 0.5 vor dem Abzug.
        var river = new DayProfile(Profiles.Of((0, 12)), ProfileSource.RiverStation);
        var inputs = new DayInputs(Calm with { WindMaxKmh = 60 }, river, ClarityEstimate.Unknown, AlgaeLevel.Unknown, 5.0, IsRiver: true);
        var rules = DiveIndexRules.Default with { MaxIndexWithoutClarity = 0.5 };

        // Act
        var result = DiveIndexCalculator.Calculate(inputs, rules);

        // Assert
        Assert.Equal(0.5, result.DiveIndex);
    }

    [Fact]
    public void Am_See_gibt_viel_Zufluss_keinen_Abzug_fuer_die_Stroemung()
    {
        // Arrange
        var inputs = new DayInputs(Calm, Summer, Clear(8), AlgaeLevel.Low, 3.0, IsRiver: false);

        // Act
        var result = DiveIndexCalculator.Calculate(inputs, DiveIndexRules.Default);

        // Assert
        Assert.DoesNotContain(ReasonCode.StrongCurrent, result.Reasons);
    }

    private static ClarityEstimate Shore(double secchi) =>
        ClarityEstimate.Choose(new DateOnly(2026, 10, 18), new SatelliteObservation(new DateOnly(2026, 10, 17), secchi, 9), null, _ => 0, 1, DiveIndexRules.Default);

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
