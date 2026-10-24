using System.Net;
using System.Text.RegularExpressions;
using Tauchplaetze.Core.Features.DiveSites.Queries;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Features.WaterBodies.Queries;

namespace Tauchplaetze.Web.Tests;

public class WaterBodiesPageTests : IClassFixture<WebApp>
{
    private readonly WebApp app;

    public WaterBodiesPageTests(WebApp app)
    {
        this.app = app;
        app.WaterBodyGroups.Answer = _ =>
        [
            new WaterBodyGroup(WaterBodyGroupKind.LargeLakes, null, [Water("zuerichsee", "lac de Zurich", WaterBodyType.Lake, 45)]),
            new WaterBodyGroup(WaterBodyGroupKind.Region, SwissRegion.Mittelland, [Water("bielersee", "lac de Bienne", WaterBodyType.Lake, 7)]),
            new WaterBodyGroup(WaterBodyGroupKind.Region, SwissRegion.Ticino, [Water("lago-di-vogorno", "Lago di Vogorno", WaterBodyType.Lake, 2)]),
            new WaterBodyGroup(WaterBodyGroupKind.Rivers, null, [Water("aare", "Aar", WaterBodyType.River, 14)]),
        ];
    }

    [Fact]
    public async Task Die_Gewaesser_stehen_in_Gruppen_grosse_Seen_zuerst_dann_Regionen_dann_Fluesse_mit_Sprungnavigation()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/fr/eaux", TestContext.Current.CancellationToken);

        // Assert
        var titles = Regex.Matches(html, "class=\"water-group-title\"[^>]*>([^<]+)<").Select(m => WebUtility.HtmlDecode(m.Groups[1].Value));
        Assert.Equal(["Les grands lacs", "Espace Mittelland", "Tessin", "Rivières"], titles);
        Assert.Contains("href=\"/fr/eaux#region-ticino\"", html);
        Assert.Contains("id=\"region-ticino\"", html);
        Assert.Contains("href=\"/fr/eaux/lago-di-vogorno\"", html);
        Assert.Contains("2 sites de plongée", VisibleText.Of(html));
    }

    [Fact]
    public async Task Die_Gewaesserseite_zeigt_den_Index_Jahr_ueber_Jahr_mit_einer_Linie_pro_Jahr()
    {
        // Arrange
        app.WaterBody.Answer = _ => new WaterBodyDetails(7, "zuerichsee", "Zürichsee", WaterBodyType.Lake, 47.28, 8.6);
        app.WaterBodyHistory.Answer = _ => new WaterBodyIndexHistory(
            [
                new IndexYear(2025, [new IndexPoint(new DateOnly(2025, 10, 18), 6.0m, 6.0m, 40), new IndexPoint(new DateOnly(2025, 10, 19), 6.4m, 6.2m, 41)]),
                new IndexYear(2026, [new IndexPoint(new DateOnly(2026, 10, 18), 7.0m, 7.0m, 49), new IndexPoint(new DateOnly(2026, 10, 19), 7.6m, 7.3m, 49)]),
            ],
            []);
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/de/gewaesser/zuerichsee", TestContext.Current.CancellationToken);

        // Assert
        var text = VisibleText.Of(html);
        Assert.Contains("Jahr über Jahr", text);
        Assert.Equal(["2025", "2026"], Regex.Matches(html, "<li data-tone=\"[a-z]+\"[^>]*><span class=\"swatch\"[^>]*></span>(\\d{4})</li>").Select(m => m.Groups[1].Value));
        Assert.Equal(2, Regex.Matches(html, "class=\"line\"").Count);
    }

    [Fact]
    public async Task Ohne_Werte_fehlt_Jahr_ueber_Jahr_auf_der_Gewaesserseite()
    {
        // Arrange
        app.WaterBody.Answer = _ => new WaterBodyDetails(7, "zuerichsee", "Zürichsee", WaterBodyType.Lake, 47.28, 8.6);
        app.WaterBodyHistory.Answer = _ => new WaterBodyIndexHistory([], []);
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/de/gewaesser/zuerichsee", TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain("Jahr über Jahr", VisibleText.Of(html));
    }

    private static WaterBodySummary Water(string slug, string name, WaterBodyType type, int sites) => new(slug.Length, slug, name, type, 46.5, 8.0, sites);

    [Fact]
    public async Task Die_Gewaesserseite_nennt_Sichtweite_im_Titel_und_Zahl_der_Plaetze_und_besten_Index_in_der_Beschreibung()
    {
        // Arrange
        app.WaterBody.Answer = _ => new WaterBodyDetails(7, "zuerichsee", "Zürichsee", WaterBodyType.Lake, 47.28, 8.6);
        app.SiteList.Answer = _ =>
        [
            new DiveSiteListItem("a", "A", "Herrliberg", "zuerichsee", "Zürichsee", WaterBodyType.Lake, 47.28, 8.6, 0, 3, 6.4m),
            new DiveSiteListItem("b", "B", "Küsnacht", "zuerichsee", "Zürichsee", WaterBodyType.Lake, 47.31, 8.58, 0, 3, 7.9m),
        ];
        using var browser = app.Browser();

        // Act
        var html = WebUtility.HtmlDecode(await browser.GetStringAsync("/de/gewaesser/zuerichsee", TestContext.Current.CancellationToken));
        app.SiteList.Answer = _ => [];

        // Assert
        Assert.Contains("<title>Zürichsee: Tauchen und Sichtweite heute · tauchplaetze.ch</title>", html);
        Assert.Contains("<meta name=\"description\" content=\"Zürichsee: 2 Tauchplätze mit Karte und täglicher Tauchprognose. Bester Tauch-Index heute: 7.9/10.\"", html);
    }
}
