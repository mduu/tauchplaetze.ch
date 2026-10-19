using System.Net;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Forecasts.Queries;

namespace Tauchplaetze.Web.Tests;

public class PlatformForecastPageTests : IClassFixture<WebApp>
{
    private static readonly DateTimeOffset At = new(2026, 10, 19, 2, 30, 0, TimeSpan.Zero);

    private readonly WebApp app;

    public PlatformForecastPageTests(WebApp app)
    {
        this.app = app;
        app.SignIn.Answer = _ => new SignInOutcome(SignInStatus.Succeeded, new SignedInAccount("admin-1", "Marc", "de", [Roles.Admin]));
        app.MirrorSignIn();
    }

    [Fact]
    public async Task Die_Prognose_Laeufe_zeigen_den_laufenden_Lauf_mit_Fortschritt_den_naechsten_Lauf_und_die_Bilanz_der_Woche()
    {
        // Arrange
        var running = new ForecastRunRow(Guid.NewGuid(), At, null, ForecastRunStatus.Running, 120, 0, [], new RunProgress(318, 40, 120));
        var partial = new ForecastRunRow(Guid.NewGuid(), At.AddHours(-13), TimeSpan.FromMinutes(12), ForecastRunStatus.PartiallySucceeded, 900, 3,
            [new FailureGroup("alplakes", FailureKind.Timeout, null, 3, ["3d-zurich-riff"], ["timeout"])], null);
        app.RunOverview.Answer = query => query.AdminId == "admin-1"
            ? new ForecastRunOverview(running, At.AddHours(8.5), new RunBalance(14, 12, 1, 1), TimeSpan.FromSeconds(690), [running, partial], 2)
            : null;
        using var browser = app.Browser();
        await SignIn(browser);

        // Act
        var text = VisibleText.Of(await browser.GetStringAsync("/platform/prognose", TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("40 von 318 Plätzen, 120 Calls bisher", text);
        Assert.Contains("Nächster Lauf 19.10.2026 13:00", text);
        Assert.Contains("14 Läufe 12 erfolgreich · 1 Teilerfolg · 1 fehlgeschlagen", text);
        Assert.Contains("Mittlere Dauer 11:30", text);
        Assert.Contains("12:00", text);
    }

    [Fact]
    public async Task Ohne_Ergebnis_zeigen_die_Prognose_Laeufe_eine_Meldung_statt_eines_Ladebalkens()
    {
        // Arrange
        app.RunOverview.Answer = _ => null;
        using var browser = app.Browser();
        await SignIn(browser);

        // Act
        var html = await browser.GetStringAsync("/platform/prognose", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("data-no-data", html);
        Assert.DoesNotContain("mud-progress-linear", html);
    }

    private static async Task SignIn(HttpClient browser)
    {
        using var response = await WebApp.Submit(browser, "/de/account/sign-in", "sign-in",
            new() { ["Input.Email"] = "marc@example.org", ["Input.Password"] = "passwort" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
