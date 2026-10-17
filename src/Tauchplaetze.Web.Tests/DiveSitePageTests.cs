using System.Net;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Web.Tests;

public class DiveSitePageTests : IClassFixture<WebApp>
{
    private const string Slug = "riff-williams-herrliberg";

    private readonly WebApp app;

    public DiveSitePageTests(WebApp app)
    {
        this.app = app;
        app.DiveSites.Add(Slug, new DiveSiteLookup.Found(new DiveSiteDetails(
            Guid.NewGuid(), Slug, "Riff Williams", "zuerichsee", "Zürichsee", WaterBodyType.Lake, "Herrliberg", "ZH", 47.2849, 8.6085, 0, DateTimeOffset.UtcNow)));
        app.DiveSites.Add("alter-name", new DiveSiteLookup.Moved(Slug));
        app.DiveSites.Add("archiviert", new DiveSiteLookup.Gone());
    }

    [Theory]
    [InlineData("/de/tauchplaetze/", "Dieser Platz ist erst im Verzeichnis.")]
    [InlineData("/en/dive-sites/", "This site is only in the directory so far.")]
    [InlineData("/fr/sites-de-plongee/", "Ce site ne figure pour l'instant que dans l'annuaire.")]
    [InlineData("/it/siti-di-immersione/", "Per ora questo sito è solo nell'elenco.")]
    public async Task Die_Platzseite_zeigt_Name_Gewaesser_und_Gemeinde_unter_dem_Pfad_jeder_Sprache(string path, string directoryOnly)
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var text = VisibleText.Of(await browser.GetStringAsync(path + Slug, TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("Riff Williams", text);
        Assert.Contains("Herrliberg ZH", text);
        Assert.Contains(directoryOnly, text);
    }

    [Fact]
    public async Task Der_Pfad_einer_anderen_Sprache_ergibt_404()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        using var response = await browser.GetAsync($"/en/tauchplaetze/{Slug}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Ein_alter_Slug_leitet_dauerhaft_auf_den_aktuellen_in_derselben_Sprache_weiter()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        using var response = await browser.GetAsync("/fr/sites-de-plongee/alter-name", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal($"/fr/sites-de-plongee/{Slug}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Ein_archivierter_Platz_ergibt_410()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        using var response = await browser.GetAsync("/de/tauchplaetze/archiviert", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task Der_Sprachwechsel_fuehrt_auf_denselben_Platz_mit_uebersetztem_Pfad()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync($"/de/tauchplaetze/{Slug}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains($"href=\"/it/siti-di-immersione/{Slug}\"", html);
        Assert.Contains($"hreflang=\"en\" href=\"https://localhost/en/dive-sites/{Slug}\"", html);
    }
}
