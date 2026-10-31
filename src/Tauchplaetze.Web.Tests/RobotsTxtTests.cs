using System.Net;
using System.Text.RegularExpressions;

namespace Tauchplaetze.Web.Tests;

public class RobotsTxtTests
{
    [Fact]
    public async Task Robots_txt_ist_Text_sperrt_Redaktion_Konten_und_Versionen_in_allen_Sprachen_und_nennt_die_Sitemap()
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
        var lines = body.Split('\n').Where(l => l.Length > 0).ToList();
        Assert.Single(lines, line => line == "Disallow: /it/siti-di-immersione/*/segnala");
        Assert.Equal(lines.Count, lines.Distinct().Count());
        Assert.Contains("Sitemap: https://tauchplaetze.ch/sitemap.xml", body);
    }

    [Theory]
    [InlineData("/de/tauchplaetze/neu", true)]
    [InlineData("/en/dive-sites/new", true)]
    [InlineData("/fr/sites-de-plongee/nouveau", true)]
    [InlineData("/it/siti-di-immersione/nuovo", true)]
    [InlineData("/de/tauchplaetze/neu?wasser=zugersee", true)]
    [InlineData("/de/tauchplaetze/neue-bruecke-grenchen", false)]
    [InlineData("/en/dive-sites/newport", false)]
    [InlineData("/fr/sites-de-plongee/nouveau-port", false)]
    [InlineData("/it/siti-di-immersione/nuovo-lido", false)]
    [InlineData("/de/tauchplaetze/neue-bruecke-grenchen/bearbeiten", true)]
    public async Task Robots_txt_sperrt_die_Seite_Neuer_Platz_aber_keinen_Platz_dessen_Slug_gleich_beginnt(string path, bool blocked)
    {
        // Arrange
        await using var app = new WebApp();
        using var client = app.Browser();
        var ct = TestContext.Current.CancellationToken;

        // Act
        var body = await client.GetStringAsync("/robots.txt", ct);

        // Assert
        Assert.Equal(blocked, IsDisallowed(body, path));
    }

    private static bool IsDisallowed(string robotsTxt, string path) =>
        robotsTxt.Split('\n')
            .Where(line => line.StartsWith("Disallow: ", StringComparison.Ordinal))
            .Select(line => "^" + Regex.Escape(line["Disallow: ".Length..].Trim()).Replace(@"\*", ".*").Replace(@"\$", "$"))
            .Any(pattern => Regex.IsMatch(path, pattern));
}
