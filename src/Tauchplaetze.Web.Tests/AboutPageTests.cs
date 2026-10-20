using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Contact.Commands;
using Tauchplaetze.Core.Features.Forecasts.Queries;

namespace Tauchplaetze.Web.Tests;

public class AboutPageTests(WebApp app) : IClassFixture<WebApp>
{
    [Theory]
    [InlineData("/de/ueber", "So entsteht die Prognose", "Datenschutz")]
    [InlineData("/en/about", "How the forecast works", "Privacy")]
    [InlineData("/fr/a-propos", "Comment la prévision est calculée", "Protection des données")]
    [InlineData("/it/chi-siamo", "Come nasce la previsione", "Protezione dei dati")]
    public async Task Die_Seite_Ueber_erklaert_die_Prognose_nennt_die_Quellen_und_den_Datenschutz_in_jeder_Sprache(string path, string forecast, string privacy)
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync(path, TestContext.Current.CancellationToken);
        var text = VisibleText.Of(html);

        // Assert
        Assert.Contains(forecast, text);
        Assert.Contains(privacy, text);
        Assert.Contains("href=\"https://www.openstreetmap.org/copyright\"", html);
        Assert.Contains("href=\"https://www.alplakes.eawag.ch\"", html);
    }

    [Theory]
    [InlineData("/de/ueber", "bis 5 km von seiner Mündung", "zwischen 8 und 16 Uhr")]
    [InlineData("/en/about", "up to 5 km from its mouth", "between 8:00 and 16:00")]
    [InlineData("/fr/a-propos", "jusqu'à 5 km de son embouchure", "entre 8 h et 16 h")]
    [InlineData("/it/chi-siamo", "fino a 5 km dalla sua foce", "tra le 8 e le 16")]
    public async Task Die_Seite_Ueber_beschreibt_jeden_Teilwert_mit_Gewicht_und_den_Zahlen_der_aktuellen_Regel(string path, string inflow, string window)
    {
        // Arrange
        using var browser = app.Browser();
        using var scope = app.Services.CreateScope();
        var rules = await scope.ServiceProvider.GetRequiredService<IQueryHandler<GetForecastRulesQuery, ForecastRulesSummary>>()
            .Handle(new GetForecastRulesQuery(), TestContext.Current.CancellationToken);

        // Act
        var html = await browser.GetStringAsync(path, TestContext.Current.CancellationToken);
        var text = VisibleText.Of(html);

        // Assert
        Assert.Equal(["Clarity", "Wind", "Inflow", "Stratification"], Regex.Matches(html, "data-factor=\"(\\w+)\"").Select(m => m.Groups[1].Value));
        Assert.Equal(100, rules.ClarityWeight + rules.WindWeight + rules.InflowWeight + rules.StratificationWeight);
        Assert.Contains($"{rules.ClarityWeight} %", text);
        Assert.Contains(rules.ModelVersion, text);
        Assert.Contains(inflow, text);
        Assert.Contains(window, text);
    }

    [Fact]
    public async Task Links_zu_Abschnitten_tragen_den_Pfad_der_Seite_weil_base_href_sonst_auf_die_Startseite_zeigt()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/de/ueber", TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotMatch("<a [^>]*href=\"#", html);
        Assert.Contains("href=\"/de/ueber#kontakt\"", html);
        Assert.Contains("href=\"/de/ueber#main\"", html);
    }

    [Fact]
    public async Task Canonical_und_Sprachversionen_der_Seite_Ueber_haben_keine_Parameter_und_die_Beschreibung_bleibt()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/fr/a-propos?bezug=Riff%20Williams&kontakt=gesendet", TestContext.Current.CancellationToken);

        // Assert
        Assert.Matches("<link rel=\"canonical\" href=\"https://[^\"?]+/fr/a-propos\"", html);
        Assert.Matches("<link rel=\"alternate\" hreflang=\"de\" href=\"https://[^\"?]+/de/ueber\"", html);
        Assert.Matches("<link rel=\"alternate\" hreflang=\"it\" href=\"https://[^\"?]+/it/chi-siamo\"", html);
        Assert.Contains("<meta name=\"description\" content=\"Comment la prévision", WebUtility.HtmlDecode(html));
    }

    [Fact]
    public async Task Das_Impressum_zeigt_die_Adresse_und_statt_einer_E_Mail_das_Kontaktformular()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/de/ueber", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Panoramastrasse 34", html);
        Assert.Contains("id=\"kontakt\"", html);
        Assert.DoesNotContain(WebApp.OperatorEmail, html);
        Assert.DoesNotContain("betreiber@", html);
        Assert.DoesNotContain("mailto:", html);
    }

    [Fact]
    public async Task Eine_Nachricht_aus_dem_Formular_geht_an_den_Core_und_die_Seite_meldet_gesendet()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var response = await Post(browser, Stamp(TimeSpan.FromMinutes(2)), trap: "");

        // Assert
        Assert.EndsWith("/de/ueber?kontakt=gesendet#kontakt", response.Headers.Location?.OriginalString);
        Assert.Equal(("anna@example.org", "Riff Williams"), (app.Contact.LastQuery?.Email, app.Contact.LastQuery?.Reference));
    }

    [Theory]
    [InlineData(120, "https://bot.example")]
    [InlineData(1, "")]
    public async Task Ein_ausgefuelltes_verstecktes_Feld_oder_ein_zu_schnelles_Absenden_sendet_nichts_und_meldet_trotzdem_gesendet(int secondsAgo, string trap)
    {
        // Arrange
        using var bot = app.Browser();
        var before = app.Contact.LastQuery;

        // Act
        var response = await Post(bot, Stamp(TimeSpan.FromSeconds(secondsAgo)), trap);

        // Assert
        Assert.EndsWith("/de/ueber?kontakt=gesendet#kontakt", response.Headers.Location?.OriginalString);
        Assert.Same(before, app.Contact.LastQuery);
    }

    [Fact]
    public async Task Nach_einem_Fehler_bleibt_der_Stempel_gueltig_und_ein_zweiter_Versuch_sofort_danach_sendet()
    {
        // Arrange
        using var browser = app.Browser();
        var calls = 0;
        app.Contact.Answer = _ => ++calls == 1 ? new ContactResult([ContactError.InvalidEmail]) : new ContactResult([]);
        var first = await Post(browser, Stamp(TimeSpan.FromMinutes(2)), trap: "");
        var html = await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var kept = WebUtility.HtmlDecode(Regex.Match(html, "name=\"Input.Stamp\" value=\"([^\"]+)\"").Groups[1].Value);

        // Act
        var second = await Post(browser, kept, trap: "");

        // Assert
        app.Contact.Answer = _ => new ContactResult([]);
        Assert.Contains("gültige E-Mail-Adresse", VisibleText.Of(html));
        Assert.EndsWith("?kontakt=gesendet#kontakt", second.Headers.Location?.OriginalString);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Ein_manipulierter_Stempel_gilt_als_abgelaufen_und_sendet_nichts()
    {
        // Arrange
        using var browser = app.Browser();
        var before = app.Contact.LastQuery;

        // Act
        var response = await Post(browser, "gefaelscht", trap: "");

        // Assert
        Assert.Contains("Das Formular ist abgelaufen.", VisibleText.Of(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
        Assert.Same(before, app.Contact.LastQuery);
    }

    [Fact]
    public async Task Der_sechste_Versuch_in_einer_Stunde_landet_beim_Limit_auch_mit_wechselndem_Cloudflare_Header()
    {
        // Arrange
        using var limited = app.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Contact:MessagesPerHour"] = "5" })));
        using var browser = limited.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var stamp = StampOf(limited.Services, TimeSpan.FromMinutes(2));
        var responses = new List<HttpResponseMessage>();

        // Act
        for (var attempt = 1; attempt <= 6; attempt++)
        {
            browser.DefaultRequestHeaders.Remove("CF-Connecting-IP");
            browser.DefaultRequestHeaders.Add("CF-Connecting-IP", $"203.0.113.{attempt}");
            responses.Add(await Post(browser, stamp, trap: "bot"));
        }

        // Assert
        Assert.All(responses.Take(5), r => Assert.EndsWith("?kontakt=gesendet#kontakt", r.Headers.Location?.OriginalString));
        Assert.EndsWith("?kontakt=limit#kontakt", responses[5].Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Ein_Platz_im_Verzeichnis_verlinkt_das_Formular_mit_seinem_Namen_als_Bezug()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/de/ueber?bezug=Riff%20Williams", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("value=\"Riff Williams\"", html);
    }

    [Fact]
    public async Task Ohne_vollstaendige_Angaben_zum_Betreiber_steht_Angaben_folgen()
    {
        // Arrange
        using var incomplete = app.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Operator:Name"] = "" })));
        using var browser = incomplete.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        // Act
        var html = await browser.GetStringAsync("/de/ueber", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Angaben folgen.", VisibleText.Of(html));
        Assert.DoesNotContain("Panoramastrasse", html);
    }

    [Fact]
    public async Task Die_Startseite_verlinkt_die_Erklaerung_der_Prognose_statt_sie_selbst_zu_zeigen()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/fr", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("href=\"/fr/a-propos#prognose\"", html);
        Assert.DoesNotContain("Pas besoin de signalements", VisibleText.Of(html));
    }

    private string Stamp(TimeSpan age) => StampOf(app.Services, age);

    private static string StampOf(IServiceProvider services, TimeSpan age) =>
        services.GetRequiredService<IDataProtectionProvider>().CreateProtector("Tauchplaetze.ContactForm.Stamp")
            .Protect((DateTimeOffset.UtcNow - age).UtcTicks.ToString(CultureInfo.InvariantCulture));

    private static Task<HttpResponseMessage> Post(HttpClient browser, string stamp, string trap) =>
        WebApp.Submit(browser, "/de/ueber", "contact", new Dictionary<string, string>
        {
            ["Input.Email"] = "anna@example.org",
            ["Input.Message"] = "Beim Riff Williams fehlt der zweite Einstieg.",
            ["Input.Reference"] = "Riff Williams",
            ["Input.Stamp"] = stamp,
            ["Input.Website"] = trap,
        }, TestContext.Current.CancellationToken);
}
