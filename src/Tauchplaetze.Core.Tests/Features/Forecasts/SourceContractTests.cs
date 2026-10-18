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
    public void Die_Hydrodaten_kommen_zeitlich_sortiert()
    {
        // Act
        var values = HydroValue.ParseList(TestData.Read("hydro", "daterange-2104-flow.json"));

        // Assert
        Assert.Equal(4367, values.Count);
        Assert.Equal(14.93, values[^1].Value);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1792241400), values[^1].Time);
    }
}
