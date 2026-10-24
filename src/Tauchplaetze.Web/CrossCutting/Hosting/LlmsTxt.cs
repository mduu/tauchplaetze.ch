using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Web.CrossCutting.Localization;

namespace Tauchplaetze.Web.CrossCutting.Hosting;

/// <summary>`/llms.txt` nach llmstxt.org (`seo.md`, «LLMs und KI-Crawler»): kurze Beschreibung und die wichtigsten Seiten für Sprachmodelle.</summary>
internal static class LlmsTxt
{
    public static IEndpointRouteBuilder MapLlmsTxt(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/llms.txt", (HttpContext context, IOptions<PublicSiteOptions> site) =>
        {
            context.Response.Headers.CacheControl = "public, max-age=86400";
            return Results.Text(Content(site.Value), "text/plain; charset=utf-8");
        });
        return endpoints;
    }

    public static string Content(PublicSiteOptions site) => $"""
        # tauchplaetze.ch

        > Tauchplatzführer für die Schweizer Seen und Flüsse mit täglicher Tauchprognose aus offenen Daten. Für jeden Platz: Tauch-Index von 0 bis 10, Sicht in Ufernähe, Sprungschicht, Temperatur nach Tiefe, Wind, Abfluss am Fluss, Karte und Einstiege. Nicht kommerziell.

        Die Website gibt es auf Deutsch, Englisch, Französisch und Italienisch. Jede Seite hat ihre Adresse pro Sprache (`/de`, `/en`, `/fr`, `/it`). Die Prognose ist ein Indikator, keine Garantie für die Sicht.

        ## Seiten

        - [Wo tauchen wir heute? (Deutsch)]({site.Link("/de")}): Rangliste der Plätze mit der besten Prognose für die nächsten 5 Tage
        - [Where shall we dive today? (English)]({site.Link("/en")})
        - [Où plonger aujourd'hui ? (Français)]({site.Link("/fr")})
        - [Dove ci immergiamo oggi? (Italiano)]({site.Link("/it")})
        - [Tauchplätze]({site.Link($"/de/{PublicRoutes.DiveSites["de"]}")}): Verzeichnis aller Plätze mit Karte
        - [Gewässer]({site.Link($"/de/{PublicRoutes.WaterBodies["de"]}")}): Seen und Flüsse mit Plätzen und Sichtweite heute

        ## Methode

        - [So entsteht die Prognose]({site.Link(PublicRoutes.AboutPage("de", "prognose"))}): Teilwerte des Tauch-Index mit Gewicht (Klarheit, Wind, Zuflüsse, Schichtung), Regeln und Grenzen

        ## Datenquellen

        - [Eawag Alplakes](https://www.alplakes.eawag.ch): Seemodelle (Temperatur nach Tiefe, Strömung) und Satellitendaten (Sicht, Algen)
        - [MeteoSchweiz](https://www.meteoswiss.admin.ch) und DWD über [Open-Meteo](https://open-meteo.com): Wetter
        - [Bundesamt für Umwelt BAFU](https://www.hydrodaten.admin.ch/de): Abfluss, Wassertemperatur und Abflussvorhersage, gemessene Werte über [existenz.ch](https://existenz.ch)
        - [swisstopo](https://www.swisstopo.admin.ch): Landeskarte, Luftbild, Tiefenlinien (swissBATHY3D)

        ## Optional

        - [Sitemap]({site.Link("/sitemap.xml")}): alle Plätze und Gewässer in allen Sprachen
        """;
}
