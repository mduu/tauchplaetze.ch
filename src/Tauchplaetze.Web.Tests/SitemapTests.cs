using System.Net;
using System.Xml.Linq;
using Tauchplaetze.Core.Features.DiveSites.Queries;

namespace Tauchplaetze.Web.Tests;

public class SitemapTests
{
    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private static readonly XNamespace Xhtml = "http://www.w3.org/1999/xhtml";

    [Fact]
    public async Task Der_Index_nennt_eine_Sitemap_pro_Sprache()
    {
        // Arrange
        await using var app = new WebApp();
        using var client = app.Browser();
        var ct = TestContext.Current.CancellationToken;

        // Act
        using var response = await client.GetAsync("/sitemap.xml", ct);
        var xml = XDocument.Parse(await response.Content.ReadAsStringAsync(ct));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            ["https://tauchplaetze.ch/sitemap-de.xml", "https://tauchplaetze.ch/sitemap-en.xml", "https://tauchplaetze.ch/sitemap-fr.xml", "https://tauchplaetze.ch/sitemap-it.xml"],
            xml.Descendants(Ns + "loc").Select(l => l.Value));
    }

    [Fact]
    public async Task Ein_Platz_steht_in_der_Sitemap_der_Sprache_mit_seinen_Fassungen_in_allen_Sprachen()
    {
        // Arrange
        await using var app = new WebApp();
        app.SitemapEntries.Answer = _ => new SitemapEntries(
            [new SitemapEntry("riff-williams-herrliberg", new DateTimeOffset(2026, 10, 18, 21, 30, 0, TimeSpan.FromHours(2)))],
            [new SitemapEntry("zuerichsee", new DateTimeOffset(2026, 10, 18, 21, 30, 0, TimeSpan.FromHours(2)))]);
        using var client = app.Browser();
        var ct = TestContext.Current.CancellationToken;

        // Act
        var xml = XDocument.Parse(await client.GetStringAsync("/sitemap-fr.xml", ct));

        // Assert
        var site = xml.Descendants(Ns + "url").Single(u => u.Element(Ns + "loc")!.Value == "https://tauchplaetze.ch/fr/sites-de-plongee/riff-williams-herrliberg");
        Assert.Equal("2026-10-18", site.Element(Ns + "lastmod")!.Value);
        Assert.Equal(
            [
                ("de", "https://tauchplaetze.ch/de/tauchplaetze/riff-williams-herrliberg"),
                ("en", "https://tauchplaetze.ch/en/dive-sites/riff-williams-herrliberg"),
                ("fr", "https://tauchplaetze.ch/fr/sites-de-plongee/riff-williams-herrliberg"),
                ("it", "https://tauchplaetze.ch/it/siti-di-immersione/riff-williams-herrliberg"),
                ("x-default", "https://tauchplaetze.ch/de/tauchplaetze/riff-williams-herrliberg"),
            ],
            site.Elements(Xhtml + "link").Select(l => ((string)l.Attribute("hreflang")!, (string)l.Attribute("href")!)));
        Assert.Contains(xml.Descendants(Ns + "loc"), l => l.Value == "https://tauchplaetze.ch/fr/eaux/zuerichsee");
        Assert.DoesNotContain(xml.Descendants(Ns + "loc"), l => l.Value.Contains("/versions", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Eine_unbekannte_Sprache_hat_keine_Sitemap()
    {
        // Arrange
        await using var app = new WebApp();
        using var client = app.Browser();

        // Act
        using var response = await client.GetAsync("/sitemap-es.xml", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
