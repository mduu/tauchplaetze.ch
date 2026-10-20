using Tauchplaetze.Web.CrossCutting.Security;

namespace Tauchplaetze.Web.Tests;

public class SecurityHeaderTests(WebApp app) : IClassFixture<WebApp>
{
    [Theory]
    [InlineData("/de")]
    [InlineData("/de/account/sign-in")]
    [InlineData("/gibt-es-nicht")]
    public async Task Jede_Antwort_verbietet_Framing_und_Sniffing_und_kuerzt_den_Referrer(string path)
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        using var response = await browser.GetAsync(path, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal(SecurityHeaders.ContentSecurityPolicy, response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains("geolocation=(self)", response.Headers.GetValues("Permissions-Policy").Single(), StringComparison.Ordinal);
    }
}
