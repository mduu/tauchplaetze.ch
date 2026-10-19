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
