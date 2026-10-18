using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Tauchplaetze.Web.Localization;

namespace Tauchplaetze.Web.Features.Forecasts;

/// <summary>Quellenangaben mit Links (`data-sources.md`). Text und Adressen kommen aus den Ressourcen, die Vorlage hat Platzhalter für die Links.</summary>
internal static class SourceLinks
{
    public static (string Text, string Url) Alplakes(IStringLocalizer<ForecastStrings> l) => (l["Source_Alplakes"], l["Source_Alplakes_Url"]);

    public static (string Text, string Url) MeteoSwiss(IStringLocalizer<ForecastStrings> l) => (l["Source_MeteoSwiss"], l["Source_MeteoSwiss_Url"]);

    public static (string Text, string Url) OpenMeteo(IStringLocalizer<ForecastStrings> l) => (l["Source_OpenMeteo"], l["Source_OpenMeteo_Url"]);

    public static (string Text, string Url) Bafu(IStringLocalizer<ForecastStrings> l) => (l["Source_Bafu"], l["Source_Bafu_Url"]);

    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    /// <summary>Kodiert die Vorlage und setzt für {0}, {1} … je einen Link ein.</summary>
    public static MarkupString Format(string template, params (string Text, string Url)[] links)
    {
        var encoder = Encoder;
        var anchors = links
            .Select(link => $"<a href=\"{encoder.Encode(link.Url)}\" rel=\"noopener\" target=\"_blank\">{encoder.Encode(link.Text)}</a>")
            .Cast<object>()
            .ToArray();
        return new MarkupString(string.Format(System.Globalization.CultureInfo.InvariantCulture, encoder.Encode(template), anchors));
    }
}
