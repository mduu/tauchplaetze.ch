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
        app.WaterBodyList.Answer = _ => [new WaterBodySummary(1, "zuerichsee", "Zürichsee", WaterBodyType.Lake), new WaterBodySummary(3, "rhein", "Rhein", WaterBodyType.River)];
        app.SiteList.Answer = query => query.WaterBodySlug is null or "zuerichsee"
            ? [new DiveSiteListItem("riff-williams-herrliberg", "Riff Williams", "Herrliberg", "zuerichsee", "Zürichsee", WaterBodyType.Lake, 47.2849, 8.6085, 0, 3, 6.5m)]
            : [];
        app.WaterBody.Answer = query => query.Slug == "zuerichsee" ? new WaterBodyDetails(1, "zuerichsee", "Zürichsee", WaterBodyType.Lake, 47.26, 8.68) : null;
    }

    [Theory]
    [InlineData("/de/tauchplaetze", "Daten am Platz")]
    [InlineData("/en/dive-sites", "data at the site")]
    public async Task Das_Verzeichnis_zeigt_Plaetze_mit_Index_und_Karte(string path, string dataLevel)
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync(path, TestContext.Current.CancellationToken);

        // Assert
        var text = VisibleText.Of(html);
        Assert.Contains("Riff Williams", text);
        Assert.Contains(dataLevel, text);
        Assert.Contains("data-map", html);
    }

    [Fact]
    public async Task Der_Filter_nach_Gewaesser_kommt_aus_der_URL()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        await browser.GetStringAsync("/de/tauchplaetze?gewaesser=rhein", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("rhein", app.SiteList.LastQuery!.WaterBodySlug);
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
