namespace Tauchplaetze.Web.Tests;

public class PartnerHiddenTests(WebApp app) : IClassFixture<WebApp>
{
    [Fact]
    public async Task Bis_zur_Freigabe_fehlt_die_Partnerseite_ganz_im_Fuss_in_der_Sitemap_und_auf_der_Seite_Schifffahrt()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var page = await browser.GetAsync("/de/partner", TestContext.Current.CancellationToken);
        var shipping = await browser.GetStringAsync("/de/schifffahrt", TestContext.Current.CancellationToken);
        var sitemap = await browser.GetStringAsync("/sitemap-de.xml", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.NotFound, page.StatusCode);
        Assert.DoesNotContain("/de/partner", shipping);
        Assert.Contains("href=\"https://www.susv.ch\"", shipping);
        Assert.DoesNotContain("/de/partner", sitemap);
    }
}
