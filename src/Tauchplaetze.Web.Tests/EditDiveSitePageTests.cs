using System.Net;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Editorial.Queries;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Web.Tests;

public class EditDiveSitePageTests : IClassFixture<WebApp>
{
    private const string Slug = "chappeli-oberaegeri";

    private static readonly Guid SiteId = Guid.NewGuid();

    private readonly WebApp app;

    public EditDiveSitePageTests(WebApp app)
    {
        this.app = app;
        app.SignIn.Answer = _ => new SignInOutcome(SignInStatus.Succeeded, new SignedInAccount("user-1", "Lucie", "fr", [Roles.Contributor]));
        app.DiveSites.Add(Slug, new DiveSiteLookup.Found(new DiveSiteDetails(
            SiteId, Slug, "Chappeli", "aegerisee", "lac d'Ägeri", WaterBodyType.Lake, "Oberägeri", "ZG", 47.13, 8.62, 0, DateTimeOffset.UtcNow, [], [])));
        var content = new DiveSiteContent("Chappeli", Slug, 1, 47.13, 8.62, "Oberägeri", "ZG", 30, 2, 4, null, null, null, null,
            DiveSiteAccess.Shore, DiveSiteFeatures.None, false, null, null, null, false, []);
        app.ForEditing.Answer = query => query.DiveSiteId == SiteId ? new DiveSiteForEditing(SiteId, DiveSiteStatus.Published, 3, content, []) : null;
    }

    [Fact]
    public async Task Ohne_Anmeldung_fuehrt_Bearbeiten_zur_Anmeldung_und_danach_zurueck()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        using var response = await browser.GetAsync($"/de/tauchplaetze/{Slug}/bearbeiten", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/de/account/sign-in?returnUrl=%2Fde%2Ftauchplaetze%2F{Slug}%2Fbearbeiten", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Der_Bearbeiten_Modus_zeigt_die_Abschnitte_im_Layout_der_Website_mit_den_Legenden_der_Bewertungen()
    {
        // Arrange
        using var browser = app.Browser();
        await SignIn(browser);

        // Act
        var html = await browser.GetStringAsync($"/fr/sites-de-plongee/{Slug}/modifier", TestContext.Current.CancellationToken);
        var text = VisibleText.Of(html);

        // Assert
        Assert.Contains("Modifier Chappeli", text);
        Assert.Contains("Tes modifications sont transmises à la modération.", text);
        Assert.Contains("Envoyer la proposition", text);
        Assert.Contains("Nom et lieu", text);
        Assert.Contains("Textes", text);
        Assert.Contains("site-header", html);
        Assert.Contains("noindex", html);
        Assert.DoesNotMatch("<link rel=\"stylesheet\" href=\"[^\"]*MudBlazor", html);
    }

    [Fact]
    public async Task Nur_der_Bearbeiten_Modus_ist_interaktiv_die_Platzseite_bleibt_statisch()
    {
        // Arrange
        using var browser = app.Browser();
        await SignIn(browser);

        // Act
        var edit = await browser.GetStringAsync($"/de/tauchplaetze/{Slug}/bearbeiten", TestContext.Current.CancellationToken);
        var page = await browser.GetStringAsync($"/de/tauchplaetze/{Slug}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("\"type\":\"server\"", edit);
        Assert.DoesNotContain("\"type\":\"server\"", page);
    }

    [Theory]
    [InlineData("vorschlag", "Merci ! Ta proposition est chez la modération.")]
    [InlineData("online", "Enregistré et en ligne.")]
    public async Task Nach_dem_Speichern_bestaetigt_die_Platzseite_was_geschah(string saved, string expected)
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var text = VisibleText.Of(await browser.GetStringAsync($"/fr/sites-de-plongee/{Slug}?gespeichert={saved}", TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains(expected, text);
    }

    private static async Task SignIn(HttpClient browser)
    {
        using var response = await WebApp.Submit(browser, "/fr/account/sign-in", "sign-in",
            new() { ["Input.Email"] = "lucie@example.org", ["Input.Password"] = "passwort" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
