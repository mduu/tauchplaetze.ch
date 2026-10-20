using System.Net;
using System.Text.RegularExpressions;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Forecasts.Queries;

namespace Tauchplaetze.Web.Tests;

public class StartPageTests : IClassFixture<WebApp>
{
    private readonly WebApp app;

    public StartPageTests(WebApp app)
    {
        this.app = app;
        app.BestSites.Ranking =
        [
            new RankedSite("riff-williams-herrliberg", "Riff Williams", "Herrliberg", "Zürichsee", 8.4m, 3, 15.2m, 11.4m, 12m, 18m, 2, [ReasonCode.ClearBelowThermocline], null),
            new RankedSite("zweiter-platz", "Zweiter Platz", "Zug", "Zugersee", 6.1m, 2, 14m, 12m, null, null, 4, [ReasonCode.Windy], null),
        ];
    }

    [Fact]
    public async Task Die_Startseite_zeigt_die_beste_Wahl_und_die_Rangliste_mit_Link_auf_den_Platz()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/de", TestContext.Current.CancellationToken);

        // Assert
        var text = VisibleText.Of(html);
        Assert.Contains("Beste Wahl für heute Riff Williams", text);
        Assert.DoesNotContain("Prognose bisher", text);
        Assert.Contains("Zweiter Platz", text);
        Assert.Contains("href=\"/de/tauchplaetze/riff-williams-herrliberg\"", html);
    }

    [Fact]
    public async Task Ein_anderer_Tag_und_ein_Ort_kommen_aus_der_URL()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/fr?tag=2026-10-20&ort=zuerich", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new DateOnly(2026, 10, 20), app.BestSites.LastQuery!.Date);
        Assert.Equal(47.3769, app.BestSites.LastQuery.Area!.Latitude);
        Assert.Contains("Où plonger mardi ?", VisibleText.Of(html));
        Assert.Contains("Meilleur choix pour mardi", VisibleText.Of(html));
    }

    [Fact]
    public async Task Die_Startseite_merkt_sich_Ort_und_Umkreis_und_Ganze_Schweiz_loescht_die_Wahl()
    {
        // Arrange
        using var browser = app.Browser();
        await browser.GetStringAsync("/de?ort=bern&km=40", TestContext.Current.CancellationToken);

        // Act
        await browser.GetStringAsync("/de", TestContext.Current.CancellationToken);
        var remembered = app.BestSites.LastQuery!.Area;
        await browser.GetStringAsync("/de?ort=schweiz", TestContext.Current.CancellationToken);
        await browser.GetStringAsync("/de", TestContext.Current.CancellationToken);
        var forgotten = app.BestSites.LastQuery!.Area;

        // Assert
        Assert.Equal((46.9480, 40), (remembered!.Latitude, remembered.RadiusKm));
        Assert.Null(forgotten);
    }

    [Fact]
    public async Task Ein_Link_mit_Ort_hat_Vorrang_vor_dem_gemerkten_Ort()
    {
        // Arrange
        using var browser = app.Browser();
        await browser.GetStringAsync("/de?ort=bern", TestContext.Current.CancellationToken);

        // Act
        await browser.GetStringAsync("/de?ort=lugano&km=15", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((46.0037, 15), (app.BestSites.LastQuery!.Area!.Latitude, app.BestSites.LastQuery.Area.RadiusKm));
    }

    [Fact]
    public async Task Das_Cookie_fuer_den_Ort_enthaelt_nur_den_Ort_und_den_Umkreis()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var response = await browser.GetAsync("/de?ort=zuerich&km=15", TestContext.Current.CancellationToken);

        // Assert
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("tp_area=", StringComparison.Ordinal));
        Assert.StartsWith("tp_area=zuerich%3A15;", cookie);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Weitere_Plaetze_desselben_Gewaessers_stehen_aufklappbar_beim_zweiten_und_Bergseen_tragen_die_Hoehe()
    {
        // Arrange
        var ranking = app.BestSites.Ranking;
        var more = new RankedSite("dritter-platz", "Dritter Platz", "Zug", "Zugersee", 5.9m, 2, null, null, null, null, 3, [], null);
        app.BestSites.Ranking = [ranking[0] with { MountainLakeElevation = 1796 }, ranking[1] with { More = [more] }];
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/de", TestContext.Current.CancellationToken);

        // Assert
        app.BestSites.Ranking = ranking;
        var text = VisibleText.Of(html);
        Assert.Contains("+ 1 weitere am Zugersee", text);
        Assert.Contains("href=\"/de/tauchplaetze/dritter-platz\"", html);
        Assert.Contains("Bergsee · 1796 m ü. M.", text);
    }

    [Theory]
    [InlineData("/de?ort=zuerich", 25)]
    [InlineData("/de?ort=zuerich&km=15", 15)]
    [InlineData("/de?ort=zuerich&km=60", 25)]
    public async Task Der_Umkreis_ist_15_25_oder_40_km_und_sonst_25_km(string url, int radiusKm)
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        await browser.GetStringAsync(url, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(radiusKm, app.BestSites.LastQuery!.Area!.RadiusKm);
    }

    [Fact]
    public async Task Solange_nur_einige_Gewaesser_eine_Prognose_haben_nennt_die_Rangliste_sie_und_Plaetze_ohne_Prognose_haben_keinen_Rang()
    {
        // Arrange
        using var browser = app.Browser();
        app.BestSites.Coverage = new ForecastCoverage(["Rhein", "Zugersee", "Zürichsee"], 99, 319);
        app.BestSites.WithoutForecast = [new UnrankedSite("ohne-prognose", "Ohne Prognose", "Küsnacht", "Zürichsee", 6)];
        app.BestSites.WithoutForecastCount = 4;

        // Act
        var html = await browser.GetStringAsync("/de?ort=zuerich", TestContext.Current.CancellationToken);

        // Assert
        app.BestSites.Coverage = null;
        app.BestSites.WithoutForecast = [];
        app.BestSites.WithoutForecastCount = 0;
        var text = VisibleText.Of(html);
        Assert.Contains("Prognose bisher für Rhein, Zugersee und Zürichsee.", text);
        Assert.Contains("Ohne Prognose Küsnacht · Zürichsee · 6 km noch keine Prognose", text);
        Assert.Contains("und 3 weitere Plätze ohne Prognose", text);
        Assert.Equal(2, Regex.Matches(html, "class=\"rank\"").Count);
    }

    [Theory]
    [InlineData(12, 18, new[] { "12 m", "18 m" })]
    [InlineData(13, 14, new[] { "13–14 m" })]
    public async Task Die_Beste_Wahl_zeigt_die_Sprungschicht_als_Band_und_bei_engem_Band_eine_Beschriftung(int top, int bottom, string[] labels)
    {
        // Arrange
        using var browser = app.Browser();
        var ranking = app.BestSites.Ranking;
        app.BestSites.Ranking = [ranking[0] with { ThermoclineTop = top, ThermoclineBottom = bottom }, ranking[1]];

        // Act
        var html = await browser.GetStringAsync("/de", TestContext.Current.CancellationToken);

        // Assert
        app.BestSites.Ranking = ranking;
        Assert.Equal(labels, Regex.Matches(html, "<text [^>]*data-thermocline-depth[^>]*>([^<]*)</text>").Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)));
    }

    [Fact]
    public async Task Ist_der_See_der_Besten_Wahl_durchmischt_zeigt_der_Querschnitt_keine_Sprungschicht()
    {
        // Arrange
        using var browser = app.Browser();
        var ranking = app.BestSites.Ranking;
        app.BestSites.Ranking = [ranking[0] with { ThermoclineTop = null, ThermoclineBottom = null }, ranking[1]];

        // Act
        var html = await browser.GetStringAsync("/de", TestContext.Current.CancellationToken);

        // Assert
        app.BestSites.Ranking = ranking;
        Assert.Contains("Keine Sprungschicht, der See ist durchmischt", VisibleText.Of(html));
        Assert.DoesNotContain("data-thermocline-depth", html);
    }

    [Fact]
    public async Task Ohne_Plaetze_mit_Index_erklaert_die_Startseite_wann_die_Prognose_rechnet()
    {
        // Arrange
        using var browser = app.Browser();
        var ranking = app.BestSites.Ranking;
        app.BestSites.Ranking = [];

        // Act
        var html = await browser.GetStringAsync("/it", TestContext.Current.CancellationToken);

        // Assert
        app.BestSites.Ranking = ranking;
        Assert.Contains("Ancora nessun indice per questo giorno.", VisibleText.Of(html));
    }
}
