using System.Net;
using Microsoft.Net.Http.Headers;
using Tauchplaetze.Web.CrossCutting.Localization;

namespace Tauchplaetze.Web.Tests;

public class LanguageTests(WebApp app) : IClassFixture<WebApp>
{
    [Theory]
    [InlineData("fr-CH,fr;q=0.9,en;q=0.8", "fr")]
    [InlineData("en-US", "en")]
    [InlineData("es-ES,it;q=0.5", "it")]
    [InlineData("it;q=0,en;q=0.4", "en")]
    [InlineData("es", "de")]
    [InlineData("", "de")]
    public void Die_Startseite_folgt_der_ersten_unterstuetzten_Sprache_im_Browser(string acceptLanguage, string expected)
    {
        // Arrange
        var header = StringWithQualityHeaderValue.ParseList(acceptLanguage.Split(',', StringSplitOptions.RemoveEmptyEntries));

        // Act
        var language = Languages.FromAcceptLanguage(header);

        // Assert
        Assert.Equal(expected, language);
    }

    [Fact]
    public async Task Ohne_Sprache_leitet_die_Wurzel_auf_die_Sprache_des_Browsers_weiter()
    {
        // Arrange
        using var browser = app.Browser();
        browser.DefaultRequestHeaders.AcceptLanguage.ParseAdd("it-CH");

        // Act
        using var response = await browser.GetAsync("/", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/it", response.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("de", "Wo tauchen wir heute?")]
    [InlineData("en", "Where shall we dive today?")]
    [InlineData("fr", "Où plonger aujourd'hui ?")]
    [InlineData("it", "Dove ci immergiamo oggi?")]
    public async Task Die_Startseite_spricht_die_Sprache_der_URL(string language, string title)
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync($"/{language}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(title, VisibleText.Of(html));
        Assert.Contains($"<html lang=\"{language}\"", html);
    }

    [Fact]
    public async Task Jede_Seite_verweist_auf_alle_Sprachen()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/fr", TestContext.Current.CancellationToken);

        // Assert
        Assert.All(["de", "en", "fr", "it", "x-default"], hreflang => Assert.Contains($"hreflang=\"{hreflang}\"", html));
    }

    [Fact]
    public async Task Eine_unbekannte_Sprache_ergibt_404()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        using var response = await browser.GetAsync("/es", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Seite nicht gefunden", VisibleText.Of(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
    }

    [Theory]
    [InlineData("de", "en", "/en")]
    [InlineData("", "fr", "/fr")]
    [InlineData("de/gewaesser/zuerichsee?tag=2026-10-18", "it", "/it/gewaesser/zuerichsee")]
    [InlineData("admin", "en", "/en")]
    public void Der_Sprachwechsel_behaelt_die_Seite(string relativePath, string language, string expected)
    {
        // Act
        var link = LanguagePaths.Switch(relativePath, language);

        // Assert
        Assert.Equal(expected, link);
    }
}
