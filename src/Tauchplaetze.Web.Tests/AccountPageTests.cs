using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Accounts.Queries;
using Tauchplaetze.Web.Features.Accounts;

namespace Tauchplaetze.Web.Tests;

public class AccountPageTests : IClassFixture<WebApp>
{
    private readonly WebApp app;

    public AccountPageTests(WebApp app)
    {
        this.app = app;
        app.SignIn.Answer = command => command.Password == "richtiges-passwort"
            ? new SignInOutcome(SignInStatus.Succeeded, new SignedInAccount("user-1", "Anna", "de", ["Contributor"]))
            : new SignInOutcome(SignInStatus.Failed);
        app.PersonalData.Answer = _ => new PersonalData("anna@example.org", "Anna", false, "de", ["Contributor"], true, null, [], []);
    }

    [Fact]
    public async Task Nach_der_Anmeldung_fuehrt_die_Seite_zurueck_und_das_Profil_ist_offen()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        using var response = await SignIn(browser, "/de/account/sign-in?returnUrl=%2Fde%2Ftauchplaetze", "richtiges-passwort");
        using var profile = await browser.GetAsync("/de/account/profile", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/de/tauchplaetze", new Uri(new Uri("https://localhost"), response.Headers.Location!).AbsolutePath);
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Contains("Anna", VisibleText.Of(await profile.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task Ein_falsches_Passwort_zeigt_einen_Hinweis_und_setzt_kein_Cookie()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        using var response = await SignIn(browser, "/fr/account/sign-in", "falsch");
        using var profile = await browser.GetAsync("/fr/account/profile", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("E-mail ou mot de passe incorrect.", VisibleText.Of(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(HttpStatusCode.Redirect, profile.StatusCode);
        Assert.StartsWith("/fr/account/sign-in?returnUrl=", profile.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Der_vierte_Anmeldeversuch_in_einer_Stunde_landet_beim_Limit()
    {
        // Arrange
        using var limited = app.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Accounts:RateLimit:SignInPerHour"] = "3" })));
        using var browser = limited.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var responses = new List<HttpResponseMessage>();

        // Act
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            responses.Add(await SignIn(browser, "/it/account/sign-in", "falsch"));
        }

        using var limitPage = await browser.GetAsync(responses[3].Headers.Location, TestContext.Current.CancellationToken);

        // Assert
        Assert.All(responses.Take(3), r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(HttpStatusCode.SeeOther, responses[3].StatusCode);
        Assert.Equal("/it/account/sign-in?limit=1", responses[3].Headers.Location?.OriginalString);
        Assert.Contains("Troppi tentativi dalla tua connessione.", VisibleText.Of(await limitPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task Ein_gesperrtes_Konto_bekommt_dieselbe_Antwort_wie_ein_falsches_Passwort()
    {
        // Arrange
        app.SignIn.Answer = _ => new SignInOutcome(SignInStatus.LockedOut);
        using var browser = app.Browser();

        // Act
        using var response = await SignIn(browser, "/de/account/sign-in", "egal");

        // Assert
        var text = VisibleText.Of(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains("E-Mail oder Passwort stimmt nicht.", text);
    }

    [Theory]
    [InlineData("/de/tauchplaetze", "/de/tauchplaetze")]
    [InlineData("https://boese.example/", "/de")]
    [InlineData("//boese.example/", "/de")]
    [InlineData("/\\boese.example", "/de")]
    [InlineData(null, "/de")]
    public void Nach_der_Anmeldung_geht_es_nur_auf_lokale_Pfade(string? returnUrl, string expected)
    {
        // Act
        var target = AccountCookie.SafeReturnUrl(returnUrl, "/de");

        // Assert
        Assert.Equal(expected, target);
    }

    private static Task<HttpResponseMessage> SignIn(HttpClient browser, string path, string password) =>
        WebApp.Submit(browser, path, "sign-in", new() { ["Input.Email"] = "anna@example.org", ["Input.Password"] = password }, TestContext.Current.CancellationToken);
}
