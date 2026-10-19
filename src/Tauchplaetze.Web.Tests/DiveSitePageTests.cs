using Tauchplaetze.Core.Features.Media;
using System.Net;
using System.Text.RegularExpressions;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Web.Tests;

public class DiveSitePageTests : IClassFixture<WebApp>
{
    private const string Slug = "riff-williams-herrliberg";

    private const string WithForecast = "platz-mit-prognose";

    private static readonly Guid ForecastSiteId = Guid.NewGuid();

    private const string River = "strandbad-rheinfelden";

    private static readonly Guid RiverSiteId = Guid.NewGuid();

    private readonly WebApp app;

    private static readonly Guid HeroPhotoId = Guid.NewGuid();

    private static PublicPhoto Photo(Guid id, MediaSection section, string? caption, string altText) =>
        new(id, section, null, 960, 640, 18, "Anna Muster", null, caption, altText,
            [new MediaRendition(480, $"https://media.test/{(id == HeroPhotoId ? "hero" : id)}/480.webp"), new MediaRendition(960, $"https://media.test/{(id == HeroPhotoId ? "hero" : id)}/960.webp")]);

    public DiveSitePageTests(WebApp app)
    {
        this.app = app;
        app.DiveSites.Add(Slug, new DiveSiteLookup.Found(new DiveSiteDetails(
            Guid.NewGuid(), Slug, "Riff Williams", "zuerichsee", "Zürichsee", WaterBodyType.Lake, "Herrliberg", "ZH", 47.2849, 8.6085, 0, DateTimeOffset.UtcNow, [], [], OtherNames: ["Grosser Parkplatz", "Seeterrasse"])));
        app.DiveSites.Add(WithForecast, new DiveSiteLookup.Found(new DiveSiteDetails(
            ForecastSiteId, WithForecast, "Platz mit Prognose", "zuerichsee", "Zürichsee", WaterBodyType.Lake, "Herrliberg", "ZH", 47.28, 8.6, 0, DateTimeOffset.UtcNow,
            [new SiteNotice(NoticeKind.Closure, "Tauchplatz gesperrt wegen Bauarbeiten.", new DateOnly(2027, 4, 1), true), new SiteNotice(NoticeKind.Info, "Parking payant.", null, false)],
            [new SiteEntryPoint(EntryPointKind.EntryAndExit, 47.2851, 8.6081, "Treppe beim Bootshaus")],
            new SiteRatings(3, 5, null, null, 1, null),
            [new SiteSketchFeature(SketchKind.Sculpture, [new SitePoint(47.2852, 8.6080)], 12, "Madonna")],
            [Photo(HeroPhotoId, MediaSection.Underwater, "Hecht an der Felswand", "Ein Hecht vor der Felswand"), Photo(Guid.NewGuid(), MediaSection.Surroundings, null, "Steg im Abendlicht")],
            HeroPhotoId)));
        app.Forecasts.Add(ForecastSiteId, new SiteForecast(
            [Day(18, 8.4m, [ReasonCode.ClearBelowThermocline, ReasonCode.CalmLake]) with { CurrentSpeed = 0.023m, CurrentDirection = 45, CurrentLevel = CurrentLevel.Calm }, Day(19, 6.2m, [ReasonCode.Windy]), Day(20, null, [])],
            DateTimeOffset.UtcNow,
            Hours(new DateTime(2030, 1, 7))));
        app.DiveSites.Add(River, new DiveSiteLookup.Found(new DiveSiteDetails(
            RiverSiteId, River, "Strandbad", "rhein", "Rhein", WaterBodyType.River, "Rheinfelden", "AG", 47.55164, 7.7767, 0, DateTimeOffset.UtcNow, [], [])));
        app.Discharge.Answer = query => query.DiveSiteId == RiverSiteId ? RiverDischarge() : null;
        app.Forecasts.Add(RiverSiteId, new SiteForecast(
            [Day(18, 7.2m, [ReasonCode.Windy]) with { ProfileSource = ProfileSource.RiverStation, TemperatureAt10m = null, TemperatureAt20m = null, TemperatureAt30m = null, ThermoclineTop = null, ThermoclineBottom = null, SecchiDepth = null, SecchiObservedAt = null }],
            DateTimeOffset.UtcNow, Hours(new DateTime(2030, 1, 7)), WaterBodyType.River));
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
    public async Task Der_Notfall_nennt_Sanitaet_Rega_und_Polizei_zum_Antippen_und_die_Rega_als_Hotline_fuer_Tauchunfaelle()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/it/siti-di-immersione/" + Slug, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("href=\"tel:144\"", html);
        Assert.Contains("href=\"tel:1414\"", html);
        Assert.Contains("href=\"tel:117\"", html);
        Assert.Contains("«incidente subacqueo»", VisibleText.Of(html));
    }

    [Fact]
    public async Task Der_Link_zum_Bearbeiten_laedt_die_ganze_Seite_weil_der_Bearbeiten_Modus_interaktiv_ist()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/de/tauchplaetze/" + Slug, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains($"<a href=\"/de/tauchplaetze/{Slug}/bearbeiten\" rel=\"nofollow\" data-enhance-nav=\"false\"", html);
    }

    [Fact]
    public async Task Weitere_Namen_stehen_unter_dem_Namen_des_Platzes()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var text = VisibleText.Of(await browser.GetStringAsync("/fr/sites-de-plongee/" + Slug, TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("Riff Williams aussi appelé Grosser Parkplatz, Seeterrasse", text);
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
    public async Task Mit_Einstiegen_zeigt_die_Karte_die_Einstiege_statt_des_Platzpunkts()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/it/siti-di-immersione/" + WithForecast, TestContext.Current.CancellationToken);
        var text = VisibleText.Of(html);

        // Assert
        Assert.Contains("Posizione e punti d'entrata", text);
        Assert.Contains("Entrata e uscita Treppe beim Bootshaus", text);
        Assert.Contains("47.2851", html);
        Assert.DoesNotContain("data-here=", html);
    }

    [Fact]
    public async Task Fotos_erscheinen_nach_Abschnitt_mit_Alternativtext_und_Fotograf_das_Titelbild_im_Hero()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/de/tauchplaetze/" + WithForecast, TestContext.Current.CancellationToken);
        var text = VisibleText.Of(html);

        // Assert
        Assert.Contains("Unter Wasser", text);
        Assert.Contains("Über Wasser", text);
        Assert.Contains("Foto: Anna Muster · auf 18 m", text);
        Assert.Contains("Titelbild: Anna Muster", text);
        Assert.Contains("alt=\"Ein Hecht vor der Felswand\"", html);
        Assert.Contains("https://media.test/hero/480.webp 480w, https://media.test/hero/960.webp 960w", html);
    }

    [Fact]
    public async Task Die_Bedingungen_zeigen_die_Kacheln_vor_dem_Profil_die_Karte_danach_und_verlinken_die_Quellen()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/fr/sites-de-plongee/" + WithForecast, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(html.IndexOf("class=\"tiles\"", StringComparison.Ordinal) < html.IndexOf("class=\"card profile\"", StringComparison.Ordinal));
        Assert.True(html.IndexOf("class=\"sources\"", StringComparison.Ordinal) < html.IndexOf("id=\"location\"", StringComparison.Ordinal));
        Assert.Matches("href=\"https://www.meteosuisse.admin.ch\"[^>]*>MétéoSuisse</a>", html);
        Assert.Contains("href=\"https://open-meteo.com\"", html);
        Assert.Contains("href=\"https://www.alplakes.eawag.ch\"", html);
        Assert.Contains("href=\"https://www.hydrodaten.admin.ch/fr\"", html);
        Assert.Matches(@"Données : Eawag Alplakes\s*,\s*MétéoSuisse et DWD via Open-Meteo\s*,\s*OFEV\s*\.", VisibleText.Of(html));
        Assert.Single(Regex.Matches(html, "href=\"https://open-meteo.com\""));
    }

    [Fact]
    public async Task Mit_Skizze_traegt_die_Karte_die_Skizze_und_den_Hinweis_dass_sie_nicht_zur_Navigation_dient()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var withSketch = await browser.GetStringAsync("/fr/sites-de-plongee/" + WithForecast, TestContext.Current.CancellationToken);
        var withoutSketch = await browser.GetStringAsync("/fr/sites-de-plongee/" + Slug, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Croquis, pas pour la navigation.", VisibleText.Of(withSketch));
        Assert.Contains("Madonna", withSketch);
        Assert.DoesNotContain("Croquis, pas pour la navigation.", VisibleText.Of(withoutSketch));
    }

    [Fact]
    public async Task Ohne_Einstiege_zeigt_die_Karte_den_Platz_selbst()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/de/tauchplaetze/" + Slug, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("data-here=", html);
        Assert.Contains("Lage", VisibleText.Of(html));
    }

    [Fact]
    public async Task Bewertete_Skalen_erscheinen_mit_Zahl_Kurztext_und_Erlaeuterung_unbewertete_fehlen()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var text = VisibleText.Of(await browser.GetStringAsync("/fr/sites-de-plongee/" + WithForecast, TestContext.Current.CancellationToken));
        var unrated = await browser.GetStringAsync("/fr/sites-de-plongee/" + Slug, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("3 moyen", text);
        Assert.Contains("5 tombant", text);
        Assert.Contains("1 aucun", text);
        Assert.Contains("L'entrée ou la profondeur demande de l'expérience. Orientation nécessaire.", text);
        Assert.DoesNotContain("Vie aquatique", text);
        Assert.DoesNotContain("data-rating=", unrated);
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
        Assert.Contains("Wetterverlauf", VisibleText.Of(html));
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
        Assert.Contains($"hreflang=\"en\" href=\"https://tauchplaetze.ch/en/dive-sites/{Slug}\"", html);
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

    [Fact]
    public async Task Die_Stroemung_erscheint_als_Kachel_mit_Einstufung_Geschwindigkeit_und_Richtung()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/fr/sites-de-plongee/" + WithForecast, TestContext.Current.CancellationToken);
        var text = VisibleText.Of(html);

        // Assert
        Assert.Contains("Courant", text);
        Assert.Contains("calme", text);
        Assert.Contains("2 cm/s vers le nord-est · 5–30 m", text);
        Assert.Contains("rotate(45deg)", html);
    }

    private static ForecastDay Day(int october, decimal? index, ReasonCode[] reasons) => new(
        new DateOnly(2026, 10, october), index is null ? (byte)1 : (byte)3, index, index is null ? null : ProfileSource.Model3d,
        index is null ? null : 15.2m, 15.0m, 11.4m, 7.9m, 12m, 18m, 7.5m, new DateOnly(2026, 10, 15), VisibilityIndicator.Good, 18m,
        2, 9m, 15m, 1.2m, AlgaeLevel.Low, reasons);

    [Theory]
    [InlineData("/de/tauchplaetze/", "Wassertemperatur", "mittel", "steigt", "Die Moderation hat die Schwellen")]
    [InlineData("/en/dive-sites/", "Water temperature", "medium", "rising", "The moderators set the thresholds")]
    [InlineData("/fr/sites-de-plongee/", "Température de l'eau", "moyen", "en hausse", "La modération a fixé les seuils")]
    [InlineData("/it/siti-di-immersione/", "Temperatura dell'acqua", "media", "in aumento", "La moderazione ha fissato le soglie")]
    public async Task Ein_Flussplatz_zeigt_Wassertemperatur_und_Abfluss_mit_Einordnung_Trend_Station_und_Legende_in_jeder_Sprache(string path, string tile, string level, string trend, string legend)
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync(path + River, TestContext.Current.CancellationToken);
        var text = VisibleText.Of(html);

        // Assert
        Assert.Contains(tile, text);
        Assert.Contains(level, text);
        Assert.Contains(trend, text);
        Assert.Contains("Rhein – Rheinfelden", text);
        Assert.Contains(legend, text);
        Assert.Contains("data-discharge=\"Medium\"", html);
        Assert.Contains("data-water-temperature", html);
    }

    [Fact]
    public async Task Ein_Platz_am_See_zeigt_keinen_Abfluss()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/de/tauchplaetze/" + Slug, TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain("data-discharge", html);
    }

    private static SiteDischarge RiverDischarge()
    {
        var now = DateTimeOffset.UtcNow;
        var hours = Enumerable.Range(0, 48).Select(h => new DischargeHour(now.AddHours(h - 47), 300 + h)).ToList();
        return new SiteDischarge("2091", "Rhein – Rheinfelden", 347, now.AddMinutes(-10), false, DischargeLevel.Medium,
            new DischargeThresholds(200, 300, 600), true, 280, DischargeTrend.Rising, 14, hours,
            [.. Enumerable.Range(0, 7).Select(d => new DischargeDay(DateOnly.FromDateTime(now.UtcDateTime).AddDays(d - 6), 290 + d))], [],
            new WaterTemperature(16.6m, now.AddMinutes(-10), false));
    }

    [Theory]
    [InlineData("/de/tauchplaetze/", "Temperatur nach Tiefe", "See", "Algen")]
    [InlineData("/en/dive-sites/", "Temperature by depth", "lake", "Algae")]
    [InlineData("/fr/sites-de-plongee/", "Température selon la profondeur", "lac", "Algues")]
    [InlineData("/it/siti-di-immersione/", "Temperatura per profondità", "lago", "Alghe")]
    public async Task Ein_Flussplatz_zeigt_kein_Tiefenprofil_keine_Algen_und_keine_Texte_zum_See(string path, string profile, string lake, string algae)
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync(path + River, TestContext.Current.CancellationToken);
        var conditions = VisibleText.Of(Regex.Match(html, "aria-labelledby=\"conditions\"[\\s\\S]*?class=\"sources\"[\\s\\S]*?</p>").Value);

        // Assert
        Assert.DoesNotContain(profile, conditions);
        Assert.DoesNotContain(algae, conditions);
        Assert.DoesNotMatch($"(?i)\\b{lake}\\b", conditions);
        Assert.DoesNotContain("Alplakes", conditions);
    }
}
