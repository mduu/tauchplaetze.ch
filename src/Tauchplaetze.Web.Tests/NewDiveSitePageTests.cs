using System.Net;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Web.Tests;

public class NewDiveSitePageTests : IClassFixture<WebApp>
{
    private readonly WebApp app;

    public NewDiveSitePageTests(WebApp app)
    {
        this.app = app;
        app.SignIn.Answer = _ => new SignInOutcome(SignInStatus.Succeeded, new SignedInAccount("user-1", "Lucie", "fr", [Roles.Contributor]));
        app.WaterBodyList.Answer = _ => [new WaterBodySummary(1, "aegerisee", "Lac d'Ägeri", WaterBodyType.Lake, 47.12, 8.63, 3)];
    }

    [Theory]
    [InlineData("/de/tauchplaetze/neu", "%2Fde%2Ftauchplaetze%2Fneu")]
    [InlineData("/en/dive-sites/new", "%2Fen%2Fdive-sites%2Fnew")]
    [InlineData("/fr/sites-de-plongee/nouveau", "%2Ffr%2Fsites-de-plongee%2Fnouveau")]
    [InlineData("/it/siti-di-immersione/nuovo", "%2Fit%2Fsiti-di-immersione%2Fnuovo")]
    public async Task Einen_neuen_Platz_schlaegt_nur_vor_wer_angemeldet_ist(string path, string returnUrl)
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        using var response = await browser.GetAsync(path, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith($"returnUrl={returnUrl}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Die_Seite_fuer_einen_neuen_Platz_ist_interaktiv_ohne_MudBlazor_nicht_indexiert_und_bietet_die_Gewaesser_an()
    {
        // Arrange
        using var browser = app.Browser();
        await SignIn(browser);

        // Act
        var html = await browser.GetStringAsync("/fr/sites-de-plongee/nouveau", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("\"type\":\"server\"", html);
        Assert.Contains("<meta name=\"robots\" content=\"noindex\"", html);
        Assert.DoesNotContain("MudBlazor.min.css", html);
        Assert.Contains("Lac d&#x27;&#xC4;geri</option>", html);
        Assert.Contains("Envoyer la proposition", VisibleText.Of(html));
    }

    [Fact]
    public async Task Verzeichnis_und_Kontomenue_fuehren_zum_Vorschlag_eines_neuen_Platzes()
    {
        // Arrange
        using var browser = app.Browser();
        var directory = await browser.GetStringAsync("/it/siti-di-immersione", TestContext.Current.CancellationToken);
        await SignIn(browser);

        // Act
        var profile = await browser.GetStringAsync("/fr/account/proposals", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("href=\"/it/siti-di-immersione/nuovo\"", directory);
        Assert.Contains("href=\"/fr/sites-de-plongee/nouveau\"", profile);
    }

    private static async Task SignIn(HttpClient browser)
    {
        using var response = await WebApp.Submit(browser, "/fr/account/sign-in", "sign-in",
            new() { ["Input.Email"] = "lucie@example.org", ["Input.Password"] = "passwort" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
