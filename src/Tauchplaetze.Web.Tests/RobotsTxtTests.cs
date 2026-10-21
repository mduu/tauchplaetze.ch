using System.Net;

namespace Tauchplaetze.Web.Tests;

public class RobotsTxtTests
{
    [Fact]
    public async Task Robots_txt_ist_Text_und_sperrt_Redaktion_Konten_und_Versionen_in_allen_Sprachen()
    {
        // Arrange
        await using var app = new WebApp();
        using var client = app.Browser();
        var ct = TestContext.Current.CancellationToken;

        // Act
        using var response = await client.GetAsync("/robots.txt", ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Disallow: /admin", body);
        Assert.Contains("Disallow: /platform", body);
        Assert.Contains("Disallow: /it/account/", body);
        Assert.Contains("Disallow: /fr/sites-de-plongee/*/versions", body);
        Assert.Contains("Disallow: /de/tauchplaetze/*/bearbeiten", body);
        Assert.DoesNotContain("Disallow: /de/tauchplaetze\n", body);
    }
}
