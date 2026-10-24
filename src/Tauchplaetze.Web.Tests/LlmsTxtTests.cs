using System.Net;

namespace Tauchplaetze.Web.Tests;

public class LlmsTxtTests
{
    [Fact]
    public async Task Llms_txt_beschreibt_die_Website_und_verlinkt_Startseite_Methode_und_Sitemap()
    {
        // Arrange
        await using var app = new WebApp();
        using var client = app.Browser();
        var ct = TestContext.Current.CancellationToken;

        // Act
        using var response = await client.GetAsync("/llms.txt", ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("# tauchplaetze.ch\n", body);
        Assert.Contains("(https://tauchplaetze.ch/de)", body);
        Assert.Contains("(https://tauchplaetze.ch/de/ueber#prognose)", body);
        Assert.Contains("(https://tauchplaetze.ch/sitemap.xml)", body);
        Assert.Contains("Eawag Alplakes", body);
        Assert.Equal("public, max-age=86400", response.Headers.CacheControl?.ToString());
    }

    [Theory]
    [InlineData("GPTBot")]
    [InlineData("ClaudeBot")]
    [InlineData("Google-Extended")]
    [InlineData("CCBot")]
    public async Task Robots_txt_sperrt_keine_KI_Crawler(string crawler)
    {
        // Arrange
        await using var app = new WebApp();
        using var client = app.Browser();

        // Act
        var body = await client.GetStringAsync("/robots.txt", TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(crawler, body);
        Assert.Contains("# KI-Crawler (Suche, Antworten, Training) erlaubt. Siehe /llms.txt", body);
        Assert.Single(body.Split('\n'), line => line.StartsWith("User-agent:", StringComparison.Ordinal));
    }
}
