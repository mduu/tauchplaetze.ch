using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Forecasts.Sources.Alplakes;
using Tauchplaetze.Core.Features.Forecasts.Sources.Hydro;
using Tauchplaetze.Core.Features.Forecasts.Sources.OpenMeteo;

namespace Tauchplaetze.Core.Tests.Features.Forecasts;

public class SourceContractTests
{
    private static readonly DateTimeOffset Morning = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Das_3D_Modell_liefert_Profil_Zeitschritte_und_Distanz_zum_Gitterpunkt()
    {
        // Act
        var series = DepthTimeSeries.Parse(TestData.Read("alplakes", "depthtime-delft3d-flow-zurich.json"), "temperature");
        var profile = series.ProfileAt(Morning, TimeSpan.FromHours(1));

        // Assert
        Assert.Equal((32, 68), (series.Times.Count, series.Depths.Count));
        Assert.Equal(99, series.DistanceToGridPoint!.Value, 0);
        Assert.Equal(18.51, profile!.SurfaceTemperature!.Value, 2);
        Assert.Equal(13.42, profile.TemperatureAt(20)!.Value, 2);
    }

    [Theory]
    [InlineData("depthtime-delft3d-flow-zurich-uv-20261019.json", 9, 68)]
    [InlineData("depthtime-mitgcm-zug-uv-20261019.json", 9, 137)]
    public void Das_3D_Modell_liefert_die_Stroemung_nach_Osten_und_Norden_fuer_jede_Tiefe_und_Zeit(string file, int times, int depths)
    {
        // Act
        var series = DepthTimeSeries.Parse(TestData.Read("alplakes", file), "temperature");

        // Assert
        Assert.Equal((times, depths), (series.Times.Count, series.Depths.Count));
        Assert.NotNull(series.EastVelocity);
        Assert.NotNull(series.NorthVelocity);
        Assert.Equal(times * depths, series.EastVelocity.Cast<double?>().Count(v => v is not null));
        Assert.Equal(times * depths, series.NorthVelocity.Cast<double?>().Count(v => v is not null));
    }

    [Fact]
    public void Das_1D_Modell_hat_dieselbe_Form_ohne_Gitterpunkt()
    {
        // Act
        var series = DepthTimeSeries.Parse(TestData.Read("alplakes", "depthtime-1d-simstrat-upperzurich.json"), "T");

        // Assert
        Assert.Equal((16, 48), (series.Times.Count, series.Depths.Count));
        Assert.Null(series.DistanceToGridPoint);
    }

    [Fact]
    public void Ein_Profil_gibt_es_nur_mit_einem_Zeitschritt_nahe_am_gewuenschten_Zeitpunkt()
    {
        // Arrange
        var series = DepthTimeSeries.Parse(TestData.Read("alplakes", "depthtime-delft3d-flow-zurich.json"), "temperature");

        // Act
        var afterModelEnd = series.ProfileAt(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(3));

        // Assert
        Assert.Null(afterModelEnd);
    }

    [Fact]
    public void Der_Satellit_liefert_die_Secchi_Tiefe_mit_dem_neuesten_Wert_zuerst()
    {
        // Act
        var values = SatelliteValue.ParseList(TestData.Read("alplakes", "rs-timeseries-zurich-sentinel3-Zsd_lee-w1.json"));

        // Assert
        Assert.Equal(new DateOnly(2026, 9, 27), DateOnly.FromDateTime(values[0].Time.UtcDateTime));
        Assert.Equal(6.0, values[0].Median, 1);
    }

    [Fact]
    public void Open_Meteo_liefert_Stunden_in_Lokalzeit_und_fehlende_Werte_bleiben_leer()
    {
        // Act
        var ch2 = HourlyWeather.Parse(TestData.Read("open-meteo", "forecast-meteoswiss_icon_ch2.json"));
        var ch1 = HourlyWeather.Parse(TestData.Read("open-meteo", "forecast-meteoswiss_icon_ch1.json"));

        // Assert
        Assert.Equal(192, ch2.Hours.Count);
        Assert.Equal(new DateTime(2026, 10, 14, 0, 0, 0), ch2.Hours[0].LocalTime);
        Assert.All(ch2.Hours, h => Assert.NotNull(h.WindSpeedKmh));
        Assert.Equal(3, ch1.Hours.Count(h => h.WindSpeedKmh is null));
    }

    [Fact]
    public void Open_Meteo_liefert_bei_mehreren_Punkten_eine_Liste_in_der_Reihenfolge_der_Anfrage()
    {
        // Act
        var points = HourlyWeather.ParseMany(TestData.Read("open-meteo", "forecast-meteoswiss_icon_ch2-two-points-20261019.json"), 2);

        // Assert
        Assert.Equal(2, points.Count);
        Assert.All(points, p => Assert.Equal(192, p.Hours.Count));
        Assert.NotEqual(points[0].Hours[30].TemperatureC, points[1].Hours[30].TemperatureC);
    }

    [Fact]
    public void Fehlt_bei_mehreren_Punkten_einer_ist_die_Antwort_ungueltig()
    {
        // Act
        var missing = Record.Exception(() => HourlyWeather.ParseMany(TestData.Read("open-meteo", "forecast-meteoswiss_icon_ch2-two-points-20261019.json"), 3));

        // Assert
        Assert.IsType<FormatException>(missing);
    }

    [Fact]
    public void Open_Meteo_liefert_Temperatur_Wettercode_Sonne_und_Tag_pro_Stunde()
    {
        // Act
        var ch2 = HourlyWeather.Parse(TestData.Read("open-meteo", "forecast-meteoswiss_icon_ch2-hourly-20261018.json"));

        // Assert
        Assert.All(ch2.Hours, h => Assert.NotNull(h.TemperatureC));
        Assert.All(ch2.Hours, h => Assert.InRange(h.SunshineSeconds!.Value, 0, 3600));
        Assert.All(ch2.Hours, h => Assert.NotNull(WeatherSymbols.FromWmo(h.WeatherCode!.Value)));
        Assert.Contains(ch2.Hours, h => h.IsDay == true);
        Assert.Contains(ch2.Hours, h => h.IsDay == false);
    }

    [Fact]
    public void Die_Hydrodaten_kommen_zeitlich_sortiert()
    {
        // Act
        var values = HydroValue.ParseList(TestData.Read("hydro", "daterange-2104-flow.json"));

        // Assert
        Assert.Equal(4367, values.Count);
        Assert.Equal(14.93, values[^1].Value);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1792241400), values[^1].Time);
    }

    [Fact]
    public void Die_Hydrodaten_mehrerer_Parameter_ergeben_den_Abfluss_pro_Station()
    {
        // Act
        var flows = HydroValue.ParseByStation(TestData.Read("hydro", "daterange-2091-flow-height.json"), "flow");

        // Assert
        Assert.Equal(["2091"], flows.Keys);
        Assert.Equal(239, flows["2091"].Count);
        Assert.Equal((335.7, 325.82), (flows["2091"][0].Value, flows["2091"][^1].Value));
    }

    [Fact]
    public void Die_Wassertemperatur_kommt_nur_fuer_Stationen_die_sie_messen()
    {
        // Act
        var series = HydroStationSeries.Parse(TestData.Read("hydro", "daterange-2091-2605-flow-temperature.json"));

        // Assert
        Assert.Equal(["2091", "2605"], series.Flow.Keys.Order());
        Assert.Equal(["2091"], series.Temperature.Keys);
        Assert.Equal(16.62, series.Temperature["2091"][^1].Value);
    }
}
