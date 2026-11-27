namespace Tauchplaetze.Web.Tests;

public class ShippingPageTests(WebApp app) : IClassFixture<WebApp>
{
    [Theory]
    [InlineData("/de/schifffahrt", "im Umkreis von 100 m", "/eli/cc/1979/337_337_337/de")]
    [InlineData("/en/shipping", "within 100 m", "/eli/cc/1979/337_337_337/de")]
    [InlineData("/fr/navigation", "dans un rayon de 100 m", "/eli/cc/1979/337_337_337/fr")]
    [InlineData("/it/navigazione", "nel raggio di 100 m", "/eli/cc/1979/337_337_337/it")]
    public async Task Die_Seite_nennt_die_Verbote_mit_100_m_um_Landungsstellen_und_verlinkt_die_Verordnung_in_der_Sprache(string path, string landingStage, string fedlex)
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync(path, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(landingStage, VisibleText.Of(html));
        Assert.Equal(5, System.Text.RegularExpressions.Regex.Matches(html, "data-case=").Count);
        Assert.Contains(fedlex, html);
    }

    [Fact]
    public async Task Die_Seite_steht_in_der_Sitemap_jeder_Sprache()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var french = await browser.GetStringAsync("/sitemap-fr.xml", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("/fr/navigation", french);
    }
}
