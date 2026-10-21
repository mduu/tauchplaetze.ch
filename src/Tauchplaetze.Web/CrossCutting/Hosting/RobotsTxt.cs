namespace Tauchplaetze.Web.CrossCutting.Hosting;

/// <summary>`/robots.txt` (`seo.md`, «Sitemap und Robots»): Öffentliches erlaubt, Redaktion, Konten, Versionen und Query-Varianten nicht.</summary>
internal static class RobotsTxt
{
    public const string Content = """
        User-agent: *
        Disallow: /admin
        Disallow: /platform
        Disallow: /api
        Disallow: /de/account/
        Disallow: /en/account/
        Disallow: /fr/account/
        Disallow: /it/account/
        Disallow: /de/tauchplaetze/neu
        Disallow: /en/dive-sites/new
        Disallow: /fr/sites-de-plongee/nouveau
        Disallow: /it/siti-di-immersione/nuovo
        Disallow: /de/tauchplaetze/*/bearbeiten
        Disallow: /en/dive-sites/*/edit
        Disallow: /fr/sites-de-plongee/*/modifier
        Disallow: /it/siti-di-immersione/*/modifica
        Disallow: /de/tauchplaetze/*/versionen
        Disallow: /en/dive-sites/*/versions
        Disallow: /fr/sites-de-plongee/*/versions
        Disallow: /it/siti-di-immersione/*/versioni
        Disallow: /*?

        """;

    public static IEndpointRouteBuilder MapRobotsTxt(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/robots.txt", () => Results.Text(Content, "text/plain; charset=utf-8"));
        return endpoints;
    }
}
