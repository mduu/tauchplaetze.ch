using System.Net;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Editorial.Commands;
using Tauchplaetze.Core.Features.Editorial.Queries;

namespace Tauchplaetze.Web.Tests;

public class AdminPageTests : IClassFixture<WebApp>
{
    private readonly WebApp app;

    public AdminPageTests(WebApp app)
    {
        this.app = app;
        app.SignIn.Answer = _ => new SignInOutcome(SignInStatus.Succeeded, new SignedInAccount("user-1", "Lucie", "fr", [Roles.Contributor]));
    }

    [Fact]
    public async Task Ohne_Anmeldung_fuehrt_die_Redaktion_zur_Anmeldung()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        using var response = await browser.GetAsync("/admin/plaetze", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/de/account/sign-in?returnUrl=%2Fadmin%2Fplaetze", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Die_Moderation_spricht_die_Sprache_aus_dem_Profil()
    {
        // Arrange
        app.SignIn.Answer = _ => new SignInOutcome(SignInStatus.Succeeded, new SignedInAccount("mod-1", "Lucie", "fr", [Roles.Moderator]));
        using var browser = app.Browser();
        await SignIn(browser);

        // Act
        using var response = await browser.GetAsync("/admin", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Bonjour Lucie", VisibleText.Of(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task Mitwirkende_sehen_die_Vorschlaege_nicht()
    {
        // Arrange
        using var browser = app.Browser();
        await SignIn(browser);

        // Act
        using var response = await browser.GetAsync("/admin/vorschlaege", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("Vérifier les propositions", VisibleText.Of(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
    }

    [Theory]
    [InlineData("/admin/plaetze/neu")]
    [InlineData("/admin/plaetze/6f1c2a52-3c1e-4c55-9d0a-0f7f3f7b9d11")]
    public async Task Mitwirkende_bearbeiten_und_schlagen_Plaetze_auf_der_Website_vor_der_Editor_im_CMS_bleibt_der_Moderation(string path)
    {
        // Arrange
        using var browser = app.Browser();
        await SignIn(browser);

        // Act
        using var response = await browser.GetAsync(path, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Mitwirkende_sehen_ihre_Vorschlaege_im_Profil_mit_Status_und_der_Begruendung_einer_Ablehnung()
    {
        // Arrange
        app.MyProposals.Answer = query => query.UserId == "user-1"
            ? [
                new MyProposal(Guid.NewGuid(), Guid.NewGuid(), "Ermitage", 4, RevisionStatus.Pending, DateTimeOffset.UtcNow, null, "Tiefe ergänzt", null),
                new MyProposal(Guid.NewGuid(), Guid.NewGuid(), "Bocchiaplatz", 2, RevisionStatus.Rejected, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, "Quelle fehlt."),
            ]
            : [];
        using var browser = app.Browser();
        await SignIn(browser);

        // Act
        var text = VisibleText.Of(await browser.GetStringAsync("/fr/account/proposals", TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("Ermitage", text);
        Assert.Contains("Retirer", text);
        Assert.Contains("Justification de la modération: Quelle fehlt.", text);
    }

    [Fact]
    public async Task Ein_wartender_Vorschlag_laesst_sich_im_Profil_zurueckziehen()
    {
        // Arrange
        var revision = Guid.NewGuid();
        app.MyProposals.Answer = _ => [new MyProposal(revision, Guid.NewGuid(), "Ermitage", 4, RevisionStatus.Pending, DateTimeOffset.UtcNow, null, null, null, "ermitage-kuesnacht")];
        using var browser = app.Browser();
        await SignIn(browser);

        // Act
        using var response = await WebApp.Submit(browser, "/fr/account/proposals", "withdraw",
            new() { ["RevisionId"] = revision.ToString() }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new WithdrawRevisionCommand("user-1", revision), app.Withdraw.LastQuery);
        Assert.Contains("/fr/sites-de-plongee/ermitage-kuesnacht", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("/admin")]
    [InlineData("/admin/plaetze")]
    [InlineData("/admin/vorschlaege")]
    [InlineData("/admin/konten")]
    public async Task Mitwirkende_kommen_nicht_in_die_Moderation(string path)
    {
        // Arrange
        using var browser = app.Browser();
        await SignIn(browser);

        // Act
        using var response = await browser.GetAsync(path, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/platform")]
    [InlineData("/platform/konten")]
    [InlineData("/platform/prognose")]
    [InlineData("/platform/queues")]
    public async Task Die_Plattform_ist_nur_fuer_Admins_auch_nicht_fuer_die_Moderation(string path)
    {
        // Arrange
        app.SignIn.Answer = _ => new SignInOutcome(SignInStatus.Succeeded, new SignedInAccount("mod-1", "Lucie", "fr", [Roles.Moderator]));
        using var browser = app.Browser();
        await SignIn(browser);

        // Act
        using var response = await browser.GetAsync(path, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/admin/meine-vorschlaege", "/fr/account/proposals", HttpStatusCode.Redirect)]
    [InlineData("/admin/prognose", "/platform/prognose", HttpStatusCode.MovedPermanently)]
    public async Task Alte_Adressen_aus_dem_CMS_leiten_auf_den_neuen_Ort(string old, string expected, HttpStatusCode status)
    {
        // Arrange
        using var browser = app.Browser();
        await SignIn(browser);

        // Act
        using var response = await browser.GetAsync(old, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(expected, response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Die_Plattform_mit_der_Mail_Queue_ist_nur_fuer_Admins()
    {
        // Arrange
        using var anonymous = app.Browser();
        using var contributor = app.Browser();
        await SignIn(contributor);

        // Act
        using var withoutSignIn = await anonymous.GetAsync("/platform/queues", TestContext.Current.CancellationToken);
        using var asContributor = await contributor.GetAsync("/platform/queues", TestContext.Current.CancellationToken);

        // Assert
        Assert.StartsWith("/de/account/sign-in?returnUrl=%2Fplatform%2Fqueues", withoutSignIn.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.Forbidden, asContributor.StatusCode);
    }

    [Theory]
    [InlineData("/de/site/riff-williams", "/de/tauchplaetze/riff-williams")]
    [InlineData("/it/site/riff-williams", "/it/siti-di-immersione/riff-williams")]
    public async Task Der_Link_aus_Mail_und_CMS_fuehrt_auf_den_Platz_in_der_Sprache(string link, string expected)
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        using var response = await browser.GetAsync(link, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal(expected, response.Headers.Location?.OriginalString);
    }

    private static async Task SignIn(HttpClient browser)
    {
        using var response = await WebApp.Submit(browser, "/fr/account/sign-in", "sign-in",
            new() { ["Input.Email"] = "lucie@example.org", ["Input.Password"] = "passwort" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
