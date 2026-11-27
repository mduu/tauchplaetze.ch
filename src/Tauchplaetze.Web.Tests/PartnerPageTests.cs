using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tauchplaetze.Web.Features.Partner;
using System.Xml.Linq;

namespace Tauchplaetze.Web.Tests;

/// <remarks>Die Seite ist ausgeblendet, bis der SUSV den Wortlaut freigibt. Diese Tests schalten sie für ihre eigene App ein.</remarks>
public class PartnerPageTests : IClassFixture<WebApp>
{
    private readonly WebApp app;

    public PartnerPageTests(WebApp app)
    {
        this.app = app;
        app.Services.GetRequiredService<IOptions<PartnerOptions>>().Value.Visible = true;
    }

    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private static readonly XNamespace Xhtml = "http://www.w3.org/1999/xhtml";

    [Theory]
    [InlineData("/de/partner", "Schweizer Unterwasser-Sport-Verband SUSV", "Mitglied werden", "https://susv.ch/de/verband/mitglieder-partner/einzelmitglied/")]
    [InlineData("/en/partners", "Swiss Underwater Sports Federation SUSV", "Become a member", "https://susv.ch/de/verband/mitglieder-partner/einzelmitglied/")]
    [InlineData("/fr/partenaires", "Fédération Suisse de Sports Subaquatiques FSSS", "Devenir membre", "https://susv.ch/fr/la-fede/membres-partenaires/membre-individuel/")]
    [InlineData("/it/partner", "Federazione Svizzera di Sport Subacquei FSSS", "Diventa socio", "https://susv.ch/it/la-federazione/membri-partner/membro-individuale/")]
    public async Task Die_Seite_Partner_zeigt_den_SUSV_mit_dem_Link_zur_Mitgliedschaft_in_der_Sprache_der_Seite_und_ist_im_Fuss_verlinkt(
        string path, string name, string member, string memberUrl)
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync(path, TestContext.Current.CancellationToken);
        var text = VisibleText.Of(html);

        // Assert
        Assert.Contains(name, text);
        Assert.Contains(member, text);
        Assert.Contains($"href=\"{memberUrl}\"", html);
        Assert.Contains("data-partner=\"susv\"", html);
        var footer = html[html.IndexOf("<footer", StringComparison.Ordinal)..];
        Assert.Contains($"href=\"{path}\"", footer);
    }

    [Fact]
    public async Task Die_Sprachversionen_der_Seite_Partner_fuehren_auf_den_Pfad_jeder_Sprache_obwohl_Deutsch_und_Italienisch_gleich_heissen()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/it/partner", TestContext.Current.CancellationToken);

        // Assert
        Assert.Matches("<link rel=\"canonical\" href=\"https://[^\"]+/it/partner\"", html);
        Assert.Matches("<link rel=\"alternate\" hreflang=\"en\" href=\"https://[^\"]+/en/partners\"", html);
        Assert.Matches("<link rel=\"alternate\" hreflang=\"fr\" href=\"https://[^\"]+/fr/partenaires\"", html);
        Assert.Matches("<link rel=\"alternate\" hreflang=\"de\" href=\"https://[^\"]+/de/partner\"", html);
    }

    [Fact]
    public async Task Die_Seite_Partner_steht_in_der_Sitemap_mit_ihren_Fassungen_in_allen_Sprachen()
    {
        // Arrange
        using var client = app.Browser();

        // Act
        var xml = XDocument.Parse(await client.GetStringAsync("/sitemap-fr.xml", TestContext.Current.CancellationToken));

        // Assert
        var page = xml.Descendants(Ns + "url").Single(u => u.Element(Ns + "loc")!.Value == "https://tauchplaetze.ch/fr/partenaires");
        Assert.Equal(
            [
                ("de", "https://tauchplaetze.ch/de/partner"),
                ("en", "https://tauchplaetze.ch/en/partners"),
                ("fr", "https://tauchplaetze.ch/fr/partenaires"),
                ("it", "https://tauchplaetze.ch/it/partner"),
                ("x-default", "https://tauchplaetze.ch/de/partner"),
            ],
            page.Elements(Xhtml + "link").Select(l => ((string)l.Attribute("hreflang")!, (string)l.Attribute("href")!)));
    }
}
