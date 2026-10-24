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
        if (today?.DiveIndex is not { } index)
        {
            return Join(intro, l["Seo_SiteFallback"].Value);
        }

        var todaySentence = TodaySentences(l, index, today).FirstOrDefault(sentence => intro.Length + 1 + sentence.Length <= MaxDescription);
        return Join(intro, todaySentence, l["Seo_SiteFallback"].Value);
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

    /// <summary>Die Prognose von heute, zuerst mit allen Werten, dann kürzer: ohne Sprungschicht, nur der Index.</summary>
    private static IEnumerable<string> TodaySentences(IStringLocalizer l, decimal index, ForecastDay today)
    {
        var visibility = today.SecchiDepthShore is { } shore ? l["Seo_SiteVisibility", Number(shore)].Value : null;
        var thermocline = today is { ThermoclineTop: { } top, ThermoclineBottom: { } bottom } ? l["Seo_SiteThermocline", Number(top), Number(bottom)].Value : null;
        string Sentence(params string?[] parts) => string.Join(", ", parts.Where(p => p is not null)) + ".";
        var head = l["Seo_SiteToday", Number(index)].Value;
        yield return Sentence(head, visibility, thermocline);
        yield return Sentence(head, visibility);
        yield return Sentence(head);
    }

    private static string Number(decimal value) => value.ToString("0.#", CultureInfo.CurrentCulture);
}
