using System.Net;
using Tauchplaetze.Core.Features.Operations.Queries;

namespace Tauchplaetze.Web.Tests;

public class HealthTests
{
    [Fact]
    public async Task Viele_Aufrufe_von_health_ready_fragen_die_Datenbank_nur_einmal_in_30_Sekunden()
    {
        // Arrange
        await using var app = new WebApp();
        app.Readiness.Answer = _ => new Readiness(false, "last forecast run too old", null);
        using var client = app.Browser();
        var ct = TestContext.Current.CancellationToken;

        // Act
        var responses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            responses.Add((await client.GetAsync("/health/ready", ct)).StatusCode);
        }

        // Assert
        Assert.All(responses, status => Assert.Equal(HttpStatusCode.ServiceUnavailable, status));
        Assert.Equal(1, app.Readiness.Calls);
    }

    [Fact]
    public async Task Health_ready_darf_nirgends_zwischengespeichert_werden()
    {
        // Arrange
        await using var app = new WebApp();
        using var client = app.Browser();

        // Act
        using var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Alive_prueft_nur_die_App_ohne_Datenbank()
    {
        // Arrange
        await using var app = new WebApp();
        using var client = app.Browser();

        // Act
        var response = await client.GetAsync("/alive", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, app.Readiness.Calls);
    }
}
