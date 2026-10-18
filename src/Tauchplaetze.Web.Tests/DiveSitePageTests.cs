using System.Net;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Web.Tests;

public class DiveSitePageTests : IClassFixture<WebApp>
{
    private const string Slug = "riff-williams-herrliberg";

    private const string WithForecast = "platz-mit-prognose";

    private static readonly Guid ForecastSiteId = Guid.NewGuid();

    private readonly WebApp app;

    public DiveSitePageTests(WebApp app)
    {
        this.app = app;
        app.DiveSites.Add(Slug, new DiveSiteLookup.Found(new DiveSiteDetails(
            Guid.NewGuid(), Slug, "Riff Williams", "zuerichsee", "Zürichsee", WaterBodyType.Lake, "Herrliberg", "ZH", 47.2849, 8.6085, 0, DateTimeOffset.UtcNow, [], [])));
        app.DiveSites.Add(WithForecast, new DiveSiteLookup.Found(new DiveSiteDetails(
            ForecastSiteId, WithForecast, "Platz mit Prognose", "zuerichsee", "Zürichsee", WaterBodyType.Lake, "Herrliberg", "ZH", 47.28, 8.6, 0, DateTimeOffset.UtcNow,
            [new SiteNotice(NoticeKind.Closure, "Tauchplatz gesperrt wegen Bauarbeiten.", new DateOnly(2027, 4, 1), true), new SiteNotice(NoticeKind.Info, "Parking payant.", null, false)],
            [new SiteEntryPoint(EntryPointKind.EntryAndExit, 47.2851, 8.6081, "Treppe beim Bootshaus")])));
        app.Forecasts.Add(ForecastSiteId, new SiteForecast(
            [Day(18, 8.4m, [ReasonCode.ClearBelowThermocline, ReasonCode.CalmLake]), Day(19, 6.2m, [ReasonCode.Windy]), Day(20, null, [])],
            DateTimeOffset.UtcNow,
            Hours(new DateTime(2030, 1, 7))));
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
    public async Task Hinweise_stehen_vor_der_Prognose_mit_Art_Enddatum_und_deutschem_Text_als_Ersatz()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/fr/sites-de-plongee/" + WithForecast, TestContext.Current.CancellationToken);
        var text = VisibleText.Of(html);

        // Assert
        Assert.Contains("Fermé · jusqu'au 01.04.2027", text);
        Assert.Matches("lang=\"de\"[^>]*>Tauchplatz gesperrt wegen Bauarbeiten.", html);
        Assert.True(html.IndexOf("data-notice-kind=\"closure\"", StringComparison.Ordinal) < html.IndexOf("id=\"conditions\"", StringComparison.Ordinal));
        Assert.Contains("Parking payant.", text);
    }

    [Fact]
    public async Task Die_Platzseite_zeigt_die_Karte_mit_dem_Platz_und_seinen_Einstiegen()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/it/siti-di-immersione/" + WithForecast, TestContext.Current.CancellationToken);
        var text = VisibleText.Of(html);

        // Assert
        Assert.Contains("Posizione e punti d'entrata", text);
        Assert.Contains("Entrata e uscita Treppe beim Bootshaus", text);
        Assert.Contains("data-here=", html);
        Assert.Contains("47.2851", html);
    }

    [Fact]
    public async Task Die_Routenlinks_fuehren_mit_Dezimalpunkt_zum_Platz_auch_auf_Franzoesisch()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/fr/sites-de-plongee/" + Slug, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("https://www.google.com/maps/dir/?api=1&amp;destination=47.28490,8.60850", html);
        Assert.Contains("https://maps.apple.com/?daddr=47.28490,8.60850&amp;dirflg=d", html);
    }

    [Theory]
    [InlineData("/de/tauchplaetze/", "Ein guter Tag.", "Gut", "Ausblick")]
    [InlineData("/fr/sites-de-plongee/", "Une bonne journée.", "Bonne", "Tendance")]
    public async Task Mit_Prognose_zeigt_die_Seite_Index_Kacheln_und_Tage_mit_Ausblick(string path, string headline, string visibility, string outlook)
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync(path + WithForecast, TestContext.Current.CancellationToken);

        // Assert
        var text = VisibleText.Of(html);
        Assert.Contains(headline, text);
        Assert.Contains(visibility, text);
        Assert.Contains(outlook, text);
        Assert.Contains("data-index-band=\"high\"", html);
        Assert.Contains("data-outlook=\"true\"", html);
    }

    [Fact]
    public async Task Das_Wetter_pro_Stunde_erscheint_mit_einer_Zusammenfassung_pro_Tag()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/de/tauchplaetze/" + WithForecast, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Wetter stündlich", VisibleText.Of(html));
        Assert.Matches(@"<desc[^>]*>Mo 07\.01\.: 8 bis 14 °C, 2[.,]0 mm Regen, Wind bis 15 km/h\.", WebUtility.HtmlDecode(html));
        Assert.Contains("href=\"#wx-rain\"", html);
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

    private static IReadOnlyList<WeatherHourForecast> Hours(DateTime start) =>
        [.. Enumerable.Range(0, 24).Select(hour => new WeatherHourForecast(
            start.AddHours(hour),
            8 + hour / 4m,
            hour == 14 ? 2.0m : 0m,
            hour is >= 9 and <= 16 ? 30 : 0,
            hour == 14 ? WeatherSymbol.Rain : WeatherSymbol.PartlyCloudy,
            hour is >= 8 and <= 17,
            hour == 15 ? 15m : 6m,
            hour == 15 ? 28m : 10m,
            220))];

    private static ForecastDay Day(int october, decimal? index, ReasonCode[] reasons) => new(
        new DateOnly(2026, 10, october), index is null ? (byte)1 : (byte)3, index, index is null ? null : ProfileSource.Model3d,
        index is null ? null : 15.2m, 15.0m, 11.4m, 7.9m, 12m, 18m, 7.5m, new DateOnly(2026, 10, 15), VisibilityIndicator.Good, 18m,
        2, 9m, 15m, 1.2m, AlgaeLevel.Low, reasons);
}
