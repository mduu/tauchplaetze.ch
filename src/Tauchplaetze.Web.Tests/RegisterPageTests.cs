using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Tauchplaetze.Web.Tests;

public class RegisterPageTests(WebApp app) : IClassFixture<WebApp>
{
    [Theory]
    [InlineData("https://spam.example", 60)]
    [InlineData("", 1)]
    public async Task Ein_Bot_mit_gefuelltem_Honeypot_oder_zu_schnell_sieht_die_Bestaetigung_es_entsteht_aber_kein_Konto(string trap, int ageSeconds)
    {
        // Arrange
        using var browser = app.Browser();
        await browser.GetAsync("/de/account/register", TestContext.Current.CancellationToken);
        var before = app.Register.Calls;

        // Act
        var response = await Post(browser, Stamp(TimeSpan.FromSeconds(ageSeconds)), trap);

        // Assert
        Assert.Contains("Fast geschafft", VisibleText.Of(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(before, app.Register.Calls);
    }

    [Fact]
    public async Task Ein_Mensch_mit_leerem_Honeypot_legt_das_Konto_an()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var response = await Post(browser, Stamp(TimeSpan.FromMinutes(1)), trap: "");

        // Assert
        Assert.Contains("Fast geschafft", VisibleText.Of(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
        Assert.Equal("anna@example.org", app.Register.LastQuery?.Email);
    }

    [Fact]
    public async Task Ein_abgelaufenes_Formular_legt_kein_Konto_an_und_bittet_um_einen_neuen_Versuch()
    {
        // Arrange
        using var browser = app.Browser();
        var before = app.Register.Calls;

        // Act
        var response = await Post(browser, Stamp(TimeSpan.FromHours(7)), trap: "");

        // Assert
        Assert.Contains("Das Formular ist abgelaufen.", VisibleText.Of(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(before, app.Register.Calls);
    }

    private string Stamp(TimeSpan age) =>
        app.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("Tauchplaetze.RegisterPage.Stamp")
            .Protect((DateTimeOffset.UtcNow - age).UtcTicks.ToString(CultureInfo.InvariantCulture));

    private static Task<HttpResponseMessage> Post(HttpClient browser, string stamp, string trap) =>
        WebApp.Submit(browser, "/de/account/register", "register", new Dictionary<string, string>
        {
            ["Input.DisplayName"] = "Anna",
            ["Input.Email"] = "anna@example.org",
            ["Input.Password"] = "ein-langes-passwort",
            ["Input.Stamp"] = stamp,
            ["Input.Website"] = trap,
        }, TestContext.Current.CancellationToken);
}
