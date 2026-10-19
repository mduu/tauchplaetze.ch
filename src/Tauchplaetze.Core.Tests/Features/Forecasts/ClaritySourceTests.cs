using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Forecasts.Calculation;
using Tauchplaetze.Core.Features.Forecasts.Sources.Alplakes;

namespace Tauchplaetze.Core.Tests.Features.Forecasts;

public class ClaritySourceTests
{
    private static readonly DateOnly Day = new(2026, 10, 19);

    [Fact]
    public void Frisches_Sentinel_2_geht_vor_und_gilt_ohne_Uferfaktor()
    {
        // Act
        var clarity = Choose(shore: Seen(daysAgo: 4, 5.4), lake: Seen(daysAgo: 1, 9));

        // Assert
        Assert.Equal((ClaritySource.Sentinel2Shore, 5.4, 5.4), (clarity.Source, clarity.SecchiDepth, clarity.ShoreDepth));
    }

    [Fact]
    public void Ist_Sentinel_2_gealtert_gilt_der_juengere_Wert_und_Sentinel_3_mit_Uferfaktor()
    {
        // Act
        var clarity = Choose(shore: Seen(daysAgo: 10, 5.4), lake: Seen(daysAgo: 2, 8));

        // Assert
        Assert.Equal((ClaritySource.Sentinel3Lake, 8.0, 5.6), (clarity.Source, clarity.SecchiDepth, clarity.ShoreDepth));
    }

    [Fact]
    public void Ist_Sentinel_3_noch_aelter_bleibt_das_gealterte_Sentinel_2()
    {
        // Act
        var clarity = Choose(shore: Seen(daysAgo: 10, 5.4), lake: Seen(daysAgo: 12, 8));

        // Assert
        Assert.Equal((ClaritySource.Sentinel2Shore, 10), (clarity.Source, clarity.AgeDays));
    }

    [Fact]
    public void Sind_beide_zu_alt_ist_die_Klarheit_unbekannt_mit_dem_Datum_des_juengeren_Bilds()
    {
        // Act
        var clarity = Choose(shore: Seen(daysAgo: 20, 5.4), lake: Seen(daysAgo: 22, 8));

        // Assert
        Assert.Equal((VisibilityIndicator.Unknown, (double?)null, Day.AddDays(-20)), (clarity.Indicator, clarity.ShoreDepth, clarity.ObservedAt));
    }

    [Fact]
    public void Ohne_beide_Werte_ist_die_Klarheit_unbekannt()
    {
        // Act
        var clarity = Choose(shore: null, lake: null);

        // Assert
        Assert.Equal(ClarityEstimate.Unknown, clarity);
    }

    [Fact]
    public void Der_Punkt_in_Ufernaehe_liegt_100_m_vom_Platz_in_Richtung_Seemitte()
    {
        // Act
        var (latitude, longitude) = SeawardPoint.Of(47.2849, 8.6085, 47.28, 8.6, 100);

        // Assert
        Assert.Equal((47.28432, 8.60749), (latitude, longitude));
        Assert.InRange(Meters(47.2849, 8.6085, latitude, longitude), 95, 105);
    }

    [Fact]
    public void Liegt_die_Seemitte_naeher_als_die_Distanz_gilt_die_Seemitte()
    {
        // Act
        var point = SeawardPoint.Of(47.2849, 8.6085, 47.2845, 8.6082, 100);

        // Assert
        Assert.Equal((47.2845, 8.6082), point);
    }

    [Fact]
    public void Die_Regeln_heissen_rules_1_2()
    {
        // Act
        var version = DiveIndexRules.Default.ModelVersion;

        // Assert
        Assert.Equal("rules-1.2", version);
    }

    [Fact]
    public void Sentinel_2_liefert_Secchi_in_Ufernaehe_mit_dem_neuesten_Bild_zuerst()
    {
        // Act
        var values = SatelliteValue.ParseList(TestData.Read("alplakes", "rs-timeseries-zurich-sentinel2-Z490-w2.json"));

        // Assert
        Assert.Equal(new DateOnly(2026, 10, 15), DateOnly.FromDateTime(values[0].Time.UtcDateTime));
        Assert.Equal(5.5, values[0].Median, 1);
    }

    [Fact]
    public void Die_Metadaten_zeigen_welche_Seen_Sentinel_2_Z490_haben()
    {
        // Act
        var products = RemoteSensingProducts.Parse(TestData.Read("alplakes", "rs-metadata-summary.json"));

        // Assert
        Assert.True(products.Has("zurich", AlplakesClient.ShoreSatellite, AlplakesClient.ShoreSecchiVariable));
        Assert.False(products.Has("barone", AlplakesClient.ShoreSatellite, AlplakesClient.ShoreSecchiVariable));
        Assert.False(products.Has("unbekannt", AlplakesClient.ShoreSatellite, AlplakesClient.ShoreSecchiVariable));
    }

    private static ClarityEstimate Choose(SatelliteObservation? shore, SatelliteObservation? lake) =>
        ClarityEstimate.Choose(Day, shore, lake, _ => 0, 1, DiveIndexRules.Default);

    private static SatelliteObservation Seen(int daysAgo, double secchi) => new(Day.AddDays(-daysAgo), secchi);

    private static double Meters(double lat1, double lng1, double lat2, double lng2) =>
        Math.Sqrt(Math.Pow((lat2 - lat1) * 111_320, 2) + Math.Pow((lng2 - lng1) * 111_320 * Math.Cos(double.DegreesToRadians(lat1)), 2));
}
