using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.Mail;

namespace Tauchplaetze.Web.CrossCutting.Hosting;

/// <summary>`/robots.txt` (`seo.md`, «Sitemap und Robots»): Öffentliches erlaubt, Redaktion, Konten, Versionen und Query-Varianten nicht. SEO-Crawler ganz gesperrt. Am Schluss der Verweis auf die Sitemap.</summary>
internal static class RobotsTxt
{
    public const string Content = """
        # KI-Crawler (Suche, Antworten, Training) erlaubt. Siehe /llms.txt
        User-agent: *
        Disallow: /admin
        Disallow: /platform
        Disallow: /api
        Disallow: /de/account/
        Disallow: /en/account/
        Disallow: /fr/account/
        Disallow: /it/account/
        Disallow: /de/tauchplaetze/neu$
        Disallow: /en/dive-sites/new$
        Disallow: /fr/sites-de-plongee/nouveau$
        Disallow: /it/siti-di-immersione/nuovo$
        Disallow: /de/tauchplaetze/*/bearbeiten
        Disallow: /en/dive-sites/*/edit
        Disallow: /fr/sites-de-plongee/*/modifier
        Disallow: /it/siti-di-immersione/*/modifica
        Disallow: /de/tauchplaetze/*/versionen
        Disallow: /en/dive-sites/*/versions
        Disallow: /fr/sites-de-plongee/*/versions
        Disallow: /it/siti-di-immersione/*/versioni
        Disallow: /de/tauchplaetze/*/melden
        Disallow: /en/dive-sites/*/report
        Disallow: /fr/sites-de-plongee/*/signaler
        Disallow: /it/siti-di-immersione/*/segnala
        Disallow: /*?

        # SEO-Crawler (Backlinks, Rankings) gesperrt: Sie bringen keine Besucher und rufen jede Seite in allen Sprachen ab.
        User-agent: SERankingBacklinksBot
        User-agent: AhrefsBot
        User-agent: SemrushBot
        User-agent: MJ12bot
        User-agent: DotBot
        User-agent: DataForSeoBot
        User-agent: BLEXBot
        User-agent: barkrowler
        Disallow: /
        """;

    public static IEndpointRouteBuilder MapRobotsTxt(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/robots.txt", (IOptions<PublicSiteOptions> site) =>
            Results.Text($"{Content}\n\nSitemap: {site.Value.Link("/sitemap.xml")}\n", "text/plain; charset=utf-8"));
        return endpoints;
    }
}
