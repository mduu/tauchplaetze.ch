using System.Net;
using Tauchplaetze.Core.Features.DiveSites.Queries;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Features.WaterBodies.Queries;

namespace Tauchplaetze.Web.Tests;

public class DirectoryPageTests : IClassFixture<WebApp>
{
    private readonly WebApp app;

    public DirectoryPageTests(WebApp app)
    {
        this.app = app;
        app.WaterBodyList.Answer = _ => [new WaterBodySummary(1, "zuerichsee", "Zürichsee", WaterBodyType.Lake, 47.26, 8.68, 1), new WaterBodySummary(3, "rhein", "Rhein", WaterBodyType.River, 47.56, 7.59, 0)];
        app.SiteList.Answer = query => query.WaterBodySlug is null or "zuerichsee"
            ? [new DiveSiteListItem("riff-williams-herrliberg", "Riff Williams", "Herrliberg", "zuerichsee", "Zürichsee", WaterBodyType.Lake, 47.2849, 8.6085, 0, 3, 6.5m)]
            : [];
        app.WaterBody.Answer = query => query.Slug == "zuerichsee" ? new WaterBodyDetails(1, "zuerichsee", "Zürichsee", WaterBodyType.Lake, 47.26, 8.68) : null;
        app.Finder.Answer = query => new FoundDiveSites(
            [new FoundDiveSite("riff-williams-herrliberg", "Riff Williams", "Herrliberg", "ZH", "Zürichsee", 47.2849, 8.6085, 6.5m, null, 30, [SiteTrait.Wall], null)],
            31, query.Page, 2, SiteSort.Index,
            [new DiveSiteListItem("riff-williams-herrliberg", "Riff Williams", "Herrliberg", "zuerichsee", "Zürichsee", WaterBodyType.Lake, 47.2849, 8.6085, 0, 3, 6.5m)],
            318, 80, query.WaterBodySlug == "rhein" ? "Rhein" : null);
    }

    [Theory]
    [InlineData("/de/tauchplaetze", "318 Plätze an 80 Gewässern.", "Steilwand · bis 30 m", "31 Treffer")]
    [InlineData("/fr/sites-de-plongee", "318 sites dans 80 lacs et rivières.", "Tombant · jusqu'à 30 m", "31 résultats")]
    public async Task Das_Verzeichnis_zeigt_Suche_Eigenschaften_Treffer_und_Karte_mit_Gruppen(string path, string count, string tags, string results)
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync(path, TestContext.Current.CancellationToken);

        // Assert
        var text = VisibleText.Of(html);
        Assert.Contains(count, text);
        Assert.Contains("Riff Williams", text);
        Assert.Contains(tags, text);
        Assert.Contains(results, text);
        Assert.Contains("type=\"search\" name=\"q\"", html);
        Assert.Contains("data-cluster=\"true\"", html);
    }

    [Fact]
    public async Task Suche_Eigenschaften_Sortierung_und_Seite_kommen_aus_der_URL()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        await browser.GetStringAsync("/de/tauchplaetze?q=zurich&f=wall&f=beginner&f=unbekannt&sort=name&page=2", TestContext.Current.CancellationToken);

        // Assert
        var query = app.Finder.LastQuery!;
        Assert.Equal(("zurich", SiteSort.Name, 2), (query.Text, query.Sort, query.Page));
        Assert.Equal([SiteTrait.Wall, SiteTrait.Beginner], query.Traits!.Order());
    }

    [Fact]
    public async Task Der_alte_Filter_nach_Gewaesser_gilt_weiter_und_laesst_sich_entfernen()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/de/tauchplaetze?gewaesser=rhein&q=platz", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("rhein", app.Finder.LastQuery!.WaterBodySlug);
        Assert.Contains("Rhein ×", VisibleText.Of(html));
        Assert.Contains("href=\"/de/tauchplaetze?q=platz\"", html);
    }

    [Theory]
    [InlineData("?page=abc&lat=x&lng=1e999&sort=quatsch&f=%3Cb%3E")]
    [InlineData("?page=-3&lat=91&lng=8.5")]
    [InlineData("?page=99999999999999999999&lat=NaN&lng=Infinity")]
    public async Task Unsinnige_Werte_in_der_URL_werden_ignoriert_und_die_Liste_erscheint(string query)
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        using var response = await browser.GetAsync("/de/tauchplaetze" + query, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Riff Williams", VisibleText.Of(html));
        Assert.Equal((1, (double?)null, (SiteSort?)null), (app.Finder.LastQuery!.Page, app.Finder.LastQuery.Latitude, app.Finder.LastQuery.Sort));
        Assert.Empty(app.Finder.LastQuery.Traits!);
    }

    [Fact]
    public async Task Ein_unbekanntes_Gewaesser_zeigt_keinen_Chip()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/de/tauchplaetze?gewaesser=unbekannt", TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain("unbekannt", VisibleText.Of(html));
        Assert.DoesNotContain("name=\"gewaesser\"", html);
    }

    [Fact]
    public async Task Canonical_und_Sprachversionen_kommen_aus_der_Konfiguration_ohne_Suche()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/fr/sites-de-plongee?q=thoune&page=2", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("rel=\"canonical\" href=\"https://tauchplaetze.ch/fr/sites-de-plongee\"", html);
        Assert.Contains("hreflang=\"de\" href=\"https://tauchplaetze.ch/de/tauchplaetze\"", html);
        Assert.Contains("<meta name=\"description\"", html);
    }

    [Fact]
    public async Task Die_Seitenlinks_behalten_die_Suche()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/de/tauchplaetze?q=see&f=wall", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("href=\"/de/tauchplaetze?q=see&amp;f=wall&amp;page=2\"", html);
        Assert.Contains("Seite 1 von 2", VisibleText.Of(html));
    }

    [Fact]
    public async Task Die_Gewaesserseite_zeigt_ihre_Plaetze_und_ein_unbekanntes_Gewaesser_ergibt_404()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/it/acque/zuerichsee", TestContext.Current.CancellationToken);
        using var unknown = await browser.GetAsync("/it/acque/gibt-es-nicht", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Riff Williams", VisibleText.Of(html));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Die_Navigation_markiert_die_aktuelle_Seite()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/fr/eaux", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("href=\"/fr/eaux\" aria-current=\"page\"", html);
    }
}
