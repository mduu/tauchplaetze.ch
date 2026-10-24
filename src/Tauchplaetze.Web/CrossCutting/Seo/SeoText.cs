using System.Globalization;
using Microsoft.Extensions.Localization;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Forecasts;

namespace Tauchplaetze.Web.CrossCutting.Seo;

/// <summary>Das Bild für geteilte Links (`og:image`). Die Adresse darf relativ zur Website sein.</summary>
public record OgImage(string Url, int Width, int Height, string? Alt);

/// <summary>Titel und Meta-Descriptions für Suchmaschinen und geteilte Links (`seo.md`, «Titel und Beschreibungen»).</summary>
internal static class SeoText
{
    /// <summary>Google kürzt längere Beschreibungen im Suchergebnis.</summary>
    public const int MaxDescription = 160;

    /// <summary>Platz: Ort und Gewässer, dazu die Prognose von heute, falls es eine gibt.</summary>
    public static string SiteDescription(IStringLocalizer l, DiveSiteDetails site, ForecastDay? today)
    {
        var place = site.Municipality is { } municipality
            ? $"{municipality}{(site.Canton is { } canton ? " " + canton : "")}, {site.WaterBodyName}"
            : site.WaterBodyName;
        var intro = l["Seo_SiteIntro", site.Name, place].Value;
        return today?.DiveIndex is { } index
            ? Join(intro, TodaySentence(l, index, today), l["Seo_SiteFallback"].Value)
            : Join(intro, l["Seo_SiteFallback"].Value);
    }

    /// <summary>Gewässer: Zahl der Plätze und der beste Index von heute.</summary>
    public static string WaterDescription(IStringLocalizer l, string name, int sites, decimal? bestToday) =>
        Join(l["Seo_WaterDescription", name, sites].Value, bestToday is { } best ? l["Seo_WaterBest", Number(best)].Value : null);

    /// <summary>Hängt Sätze an, solange sie in <see cref="MaxDescription"/> passen. Der erste Satz kommt immer.</summary>
    public static string Join(params string?[] sentences)
    {
        var text = "";
        foreach (var sentence in sentences.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            var next = text.Length == 0 ? sentence! : $"{text} {sentence}";
            if (text.Length > 0 && next.Length > MaxDescription)
            {
                break;
            }

            text = next;
        }

        return text;
    }

    private static string TodaySentence(IStringLocalizer l, decimal index, ForecastDay today)
    {
        var parts = new List<string> { l["Seo_SiteToday", Number(index)] };
        if (today.SecchiDepthShore is { } shore)
        {
            parts.Add(l["Seo_SiteVisibility", Number(shore)]);
        }

        if (today is { ThermoclineTop: { } top, ThermoclineBottom: { } bottom })
        {
            parts.Add(l["Seo_SiteThermocline", Number(top), Number(bottom)]);
        }

        return string.Join(", ", parts) + ".";
    }

    private static string Number(decimal value) => value.ToString("0.#", CultureInfo.CurrentCulture);
}
