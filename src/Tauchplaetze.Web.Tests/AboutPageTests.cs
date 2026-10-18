using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

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
        Assert.DoesNotContain("betreiber@example.org", html);
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

    private string Stamp(TimeSpan age) =>
        app.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("Tauchplaetze.ContactForm.Stamp")
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
