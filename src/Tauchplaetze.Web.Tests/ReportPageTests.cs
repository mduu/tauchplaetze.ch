using System.Net;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Reports;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Web.Tests;

public class ReportPageTests : IClassFixture<WebApp>
{
    private const string Slug = "riff-williams-herrliberg";

    private static readonly Guid SiteId = Guid.NewGuid();

    private readonly WebApp app;

    public ReportPageTests(WebApp app)
    {
        this.app = app;
        app.DiveSites.Add(Slug, new DiveSiteLookup.Found(new DiveSiteDetails(
            SiteId, Slug, "Riff Williams", "zuerichsee", "Zürichsee", WaterBodyType.Lake, "Herrliberg", "ZH", 47.2849, 8.6085, 0, DateTimeOffset.UtcNow, [], [])));
        app.SignIn.Answer = command => command.Password == "richtiges-passwort"
            ? new SignInOutcome(SignInStatus.Succeeded, new SignedInAccount("user-1", "Anna", "de", ["Contributor"]))
            : new SignInOutcome(SignInStatus.Failed);
    }

    [Fact]
    public async Task Die_Platzseite_zeigt_Meldungen_mit_Sicht_Temperatur_und_Namen_nur_mit_Einwilligung()
    {
        // Arrange
        app.SiteReports.Answer = _ =>
        [
            new SiteReport(Guid.NewGuid(), new DateOnly(2026, 10, 20), 6.5m, 12.4m, 15, "Unter 15 m sehr klar", ReportCommentStatus.Approved, "Anna", false, 7.4m),
            new SiteReport(Guid.NewGuid(), new DateOnly(2026, 10, 19), 4m, null, null, null, ReportCommentStatus.None, null, false, null),
        ];
        using var browser = app.Browser();

        // Act
        var german = VisibleText.Of(await browser.GetStringAsync($"/de/tauchplaetze/{Slug}", TestContext.Current.CancellationToken));
        var french = VisibleText.Of(await browser.GetStringAsync($"/fr/sites-de-plongee/{Slug}", TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("Sicht 6.5 m", german);
        Assert.Contains("12.4 °C auf 15 m", german);
        Assert.Contains("20.10.2026 · Anna · Prognose: Index 7.4", german);
        Assert.Contains("«Unter 15 m sehr klar»", german);
        Assert.Contains("19.10.2026 · Taucher:in", german);
        Assert.Contains("Signaler une plongée", french);
    }

    [Fact]
    public async Task Ohne_Anmeldung_fuehrt_Tauchgang_melden_zur_Anmeldung_und_zurueck()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        using var response = await browser.GetAsync($"/de/tauchplaetze/{Slug}/melden", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/de/account/sign-in?returnUrl=%2Fde%2Ftauchplaetze%2Friff-williams-herrliberg%2Fmelden", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Das_Formular_schickt_die_Meldung_an_den_Core_und_zeigt_seine_Fehler()
    {
        // Arrange
        using var browser = app.Browser();
        await WebApp.Submit(browser, "/de/account/sign-in", "sign-in", new() { ["Input.Email"] = "anna@example.org", ["Input.Password"] = "richtiges-passwort" }, TestContext.Current.CancellationToken);
        var path = $"/de/tauchplaetze/{Slug}/melden";
        var fields = new Dictionary<string, string>
        {
            ["Input.Date"] = "2026-10-20", ["Input.Visibility"] = "6.5", ["Input.Temperature"] = "12.4", ["Input.Depth"] = "15", ["Input.Comment"] = "Sehr klar",
        };

        // Act
        using var saved = await WebApp.Submit(browser, path, "report", fields, TestContext.Current.CancellationToken);
        var savedText = VisibleText.Of(await saved.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var sent = app.SubmitReport.LastQuery!;
        app.SubmitReport.Answer = _ => new ReportResult([ReportError.InvalidDate]);
        using var failed = await WebApp.Submit(browser, path, "report", fields, TestContext.Current.CancellationToken);
        var failedText = VisibleText.Of(await failed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(("user-1", SiteId, new DateOnly(2026, 10, 20), 6.5m, (decimal?)12.4m, (int?)15, "Sehr klar"),
            (sent.UserId, sent.DiveSiteId, sent.Date, sent.Visibility, sent.Temperature, sent.TemperatureDepth, sent.Comment));
        Assert.Contains("Der Kommentar erscheint nach der Freigabe", savedText);
        Assert.Contains("Wähle heute oder einen Tag der letzten 7 Tage.", failedText);
    }
}
