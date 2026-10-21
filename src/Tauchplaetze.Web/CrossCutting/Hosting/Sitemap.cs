using System.Globalization;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.DiveSites.Queries;
using Tauchplaetze.Web.CrossCutting.Localization;

namespace Tauchplaetze.Web.CrossCutting.Hosting;

/// <summary>
/// `/sitemap.xml` als Index und `/sitemap-{sprache}.xml` pro Sprache (`seo.md`, «Sitemap und Robots»).
/// Jede Adresse nennt ihre Fassungen in allen Sprachen mit `hreflang`. Die Antwort bleibt eine Stunde im Speicher.
/// </summary>
internal static class Sitemap
{
    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private static readonly XNamespace Xhtml = "http://www.w3.org/1999/xhtml";
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(1);

    public static IEndpointRouteBuilder MapSitemap(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/sitemap.xml", (IOptions<PublicSiteOptions> site) =>
            Xml(Index(site.Value)));

        endpoints.MapGet("/sitemap-{lang}.xml", async (string lang, IMemoryCache cache, IQueryHandler<GetSitemapEntriesQuery, SitemapEntries> entries, IOptions<PublicSiteOptions> site, CancellationToken ct) =>
        {
            if (!Languages.All.Contains(lang))
            {
                return Results.NotFound();
            }

            var content = await cache.GetOrCreateAsync($"sitemap:{lang}", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = CacheFor;
                return ForLanguage(lang, await entries.Handle(new GetSitemapEntriesQuery(), ct), site.Value);
            });
            return Xml(content!);
        });
        return endpoints;
    }

    /// <summary>Der Index mit einer Sitemap pro Sprache.</summary>
    public static string Index(PublicSiteOptions site) =>
        new XDocument(new XElement(Ns + "sitemapindex",
            Languages.All.Select(l => new XElement(Ns + "sitemap", new XElement(Ns + "loc", site.Link($"/sitemap-{l}.xml")))))).ToString();

    /// <summary>Die Seiten einer Sprache: Start, Verzeichnis, Gewässer, «Über», jeder Platz und jedes Gewässer.</summary>
    public static string ForLanguage(string language, SitemapEntries entries, PublicSiteOptions site)
    {
        var pages = new List<(Func<string, string> Path, DateTimeOffset? LastModified)>
        {
            (l => $"/{l}", null),
            (l => $"/{l}/{PublicRoutes.DiveSites[l]}", null),
            (l => $"/{l}/{PublicRoutes.WaterBodies[l]}", null),
            (l => PublicRoutes.AboutPage(l), null),
        };
        pages.AddRange(entries.WaterBodies.Select(w => ((Func<string, string>)(l => $"/{l}/{PublicRoutes.WaterBodies[l]}/{w.Slug}"), (DateTimeOffset?)w.LastModified)));
        pages.AddRange(entries.DiveSites.Select(s => ((Func<string, string>)(l => PublicRoutes.DiveSite(l, s.Slug)), (DateTimeOffset?)s.LastModified)));

        return new XDocument(new XElement(Ns + "urlset",
            new XAttribute(XNamespace.Xmlns + "xhtml", Xhtml),
            pages.Select(p => Url(p.Path, p.LastModified, language, site)))).ToString();
    }

    private static XElement Url(Func<string, string> path, DateTimeOffset? lastModified, string language, PublicSiteOptions site) =>
        new(Ns + "url",
            new XElement(Ns + "loc", site.Link(path(language))),
            lastModified is { } modified ? new XElement(Ns + "lastmod", modified.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) : null,
            Languages.All.Select(l => Alternate(l, site.Link(path(l)))),
            Alternate("x-default", site.Link(path(Languages.Default))));

    private static XElement Alternate(string hreflang, string href) =>
        new(Xhtml + "link", new XAttribute("rel", "alternate"), new XAttribute("hreflang", hreflang), new XAttribute("href", href));

    private static IResult Xml(string content) => Results.Text("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + content, "application/xml; charset=utf-8");
}
