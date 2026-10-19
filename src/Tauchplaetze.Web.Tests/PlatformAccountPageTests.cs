using System.Net;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Accounts.Queries;

namespace Tauchplaetze.Web.Tests;

public class PlatformAccountPageTests : IClassFixture<WebApp>
{
    private static readonly DateTimeOffset At = new(2026, 10, 19, 14, 5, 0, TimeSpan.Zero);

    private readonly WebApp app;

    public PlatformAccountPageTests(WebApp app)
    {
        this.app = app;
        app.SignIn.Answer = _ => new SignInOutcome(SignInStatus.Succeeded, new SignedInAccount("admin-1", "Marc", "de", [Roles.Admin]));
        app.MirrorSignIn();
        app.AccountDetails.Answer = query => query is { AdminId: "admin-1", AccountId: "spam-1" }
            ? new AccountDetails("spam-1", "Gratis Uhren", "spam@example.org", false, "en", false, [Roles.Contributor], At.AddDays(-1), null,
                3, null, true, [], null, null, 2, 2, 0)
            : null;
    }

    [Fact]
    public async Task Die_Kontoseite_zeigt_einem_Admin_Registrierung_Anmeldung_Methoden_Beitraege_und_die_Aktionen()
    {
        // Arrange
        using var browser = app.Browser();
        await SignIn(browser);

        // Act
        var text = VisibleText.Of(await browser.GetStringAsync("/platform/konten/spam-1", TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("Registriert 18.10.2026 16:05", text);
        Assert.Contains("Letzte Anmeldung noch nie", text);
        Assert.Contains("Anmeldemethoden Passwort", text);
        Assert.Contains("2 Versionen, davon 2 offen · 0 Fotos", text);
        Assert.Contains("E-Mail von Hand bestätigen", text);
        Assert.Contains("Konto löschen", text);
        Assert.DoesNotContain("Link für ein neues Passwort senden", text);
    }

    [Fact]
    public async Task Ohne_Konto_oder_Recht_zeigt_die_Kontoseite_eine_Meldung()
    {
        // Arrange
        using var browser = app.Browser();
        await SignIn(browser);

        // Act
        var html = await browser.GetStringAsync("/platform/konten/unbekannt", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("data-no-data", html);
        Assert.DoesNotContain("Konto löschen", VisibleText.Of(html));
    }

    [Fact]
    public async Task Die_Kontenliste_der_Plattform_fuehrt_zur_Kontoseite()
    {
        // Arrange
        app.Accounts.Answer = _ => [new AccountSummary("spam-1", "Gratis Uhren", "spam@example.org", false, [Roles.Contributor], null)];
        using var browser = app.Browser();
        await SignIn(browser);

        // Act
        var html = await browser.GetStringAsync("/platform/konten", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("href=\"/platform/konten/spam-1\"", html);
    }

    private static async Task SignIn(HttpClient browser)
    {
        using var response = await WebApp.Submit(browser, "/de/account/sign-in", "sign-in",
            new() { ["Input.Email"] = "marc@example.org", ["Input.Password"] = "passwort" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
