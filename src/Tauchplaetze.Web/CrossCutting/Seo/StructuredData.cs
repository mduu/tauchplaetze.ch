using System.Text.Json;
using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Features.WaterBodies.Queries;
using Tauchplaetze.Web.CrossCutting.Localization;

namespace Tauchplaetze.Web.CrossCutting.Seo;

/// <summary>Strukturierte Daten nach schema.org als JSON-LD (`seo.md`, «Strukturierte Daten»).</summary>
internal static class StructuredData
{
    /// <summary>Der Standard-Encoder maskiert `&lt;`, `&gt;` und `&amp;`. Ein Name kann so das `&lt;script&gt;` nicht schliessen.</summary>
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    /// <summary>Startseite: die Website und ihr Betreiber als Marke, ohne Personendaten.</summary>
    public static string Website(PublicSiteOptions site, string language, string description, string logoUrl) => Serialize(new Dictionary<string, object>
    {
        ["@context"] = "https://schema.org",
        ["@graph"] = new object[]
        {
            new Dictionary<string, object>
            {
                ["@type"] = "WebSite",
                ["name"] = "tauchplaetze.ch",
                ["url"] = site.Link($"/{language}"),
                ["inLanguage"] = language,
                ["description"] = description,
            },
            new Dictionary<string, object>
            {
                ["@type"] = "Organization",
                ["name"] = "tauchplaetze.ch",
                ["url"] = site.Link("/"),
                ["logo"] = logoUrl,
            },
        },
    });

    /// <summary>Platz: Sehenswürdigkeit mit Koordinaten, Ort und Gewässer, dazu der Pfad Gewässer › Platz.</summary>
    public static string DiveSite(PublicSiteOptions site, string language, DiveSiteDetails details, string description, string? imageUrl, string homeName)
    {
        var url = site.Link(PublicRoutes.DiveSite(language, details.Slug));
        var waterUrl = site.Link(WaterBodyPath(language, details.WaterBodySlug));
        var place = new Dictionary<string, object>
        {
            ["@type"] = "TouristAttraction",
            ["name"] = details.Name,
            ["description"] = description,
            ["url"] = url,
            ["geo"] = new Dictionary<string, object>
            {
                ["@type"] = "GeoCoordinates",
                ["latitude"] = Math.Round(details.Latitude, 5),
                ["longitude"] = Math.Round(details.Longitude, 5),
            },
            ["address"] = Address(details.Municipality, details.Canton),
            ["containedInPlace"] = new Dictionary<string, object>
            {
                ["@type"] = BodyType(details.WaterBodyType),
                ["name"] = details.WaterBodyName,
                ["url"] = waterUrl,
            },
        };
        if (imageUrl is not null)
        {
            place["image"] = imageUrl;
        }

        return Serialize(new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = new object[] { place, Breadcrumbs((homeName, site.Link($"/{language}")), (details.WaterBodyName, waterUrl), (details.Name, url)) },
        });
    }

    /// <summary>Gewässer: See oder Fluss mit Koordinaten, dazu der Pfad Startseite › Gewässer.</summary>
    public static string WaterBody(PublicSiteOptions site, string language, WaterBodyDetails water, string description, string homeName)
    {
        var url = site.Link(WaterBodyPath(language, water.Slug));
        return Serialize(new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["@type"] = BodyType(water.Type),
                    ["name"] = water.Name,
                    ["description"] = description,
                    ["url"] = url,
                    ["geo"] = new Dictionary<string, object>
                    {
                        ["@type"] = "GeoCoordinates",
                        ["latitude"] = Math.Round(water.Latitude, 5),
                        ["longitude"] = Math.Round(water.Longitude, 5),
                    },
                },
                Breadcrumbs((homeName, site.Link($"/{language}")), (water.Name, url)),
            },
        });
    }

    private static string WaterBodyPath(string language, string slug) => $"/{language}/{PublicRoutes.WaterBodies[language]}/{slug}";

    private static string BodyType(WaterBodyType type) => type == WaterBodyType.River ? "RiverBodyOfWater" : "LakeBodyOfWater";

    private static Dictionary<string, object> Address(string? municipality, string? canton)
    {
        var address = new Dictionary<string, object> { ["@type"] = "PostalAddress", ["addressCountry"] = "CH" };
        if (municipality is not null)
        {
            address["addressLocality"] = municipality;
        }

        if (canton is not null)
        {
            address["addressRegion"] = canton;
        }

        return address;
    }

    private static Dictionary<string, object> Breadcrumbs(params (string Name, string Url)[] items) => new()
    {
        ["@type"] = "BreadcrumbList",
        ["itemListElement"] = items.Select((item, index) => new Dictionary<string, object>
        {
            ["@type"] = "ListItem",
            ["position"] = index + 1,
            ["name"] = item.Name,
            ["item"] = item.Url,
        }).ToArray(),
    };

    private static string Serialize(object data) => JsonSerializer.Serialize(data, Options);
}
