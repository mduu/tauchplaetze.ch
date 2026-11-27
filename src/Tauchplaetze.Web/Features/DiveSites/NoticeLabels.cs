using Microsoft.Extensions.Localization;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Web.Features.Forecasts;
using Tauchplaetze.Web.Localization;

namespace Tauchplaetze.Web.Features.DiveSites;

/// <summary>Zeitraum eines Hinweises als Text (`dive-site.md`, «Anzeige nach Zeitraum»).</summary>
public static class NoticeLabels
{
    /// <summary>Heute gültig: «bis 15.12.2026». Ohne Ende leer.</summary>
    public static string? Today(SiteNotice notice, IStringLocalizer<DiveSiteStrings> l) =>
        notice.ValidUntil is { } until ? l["Notice_Until", ForecastFormat.Date(until)].Value : null;

    /// <summary>Beginnt bald oder später: «jedes Jahr 15.10.–15.12.», «15.06.2027 bis 30.06.2027» oder «ab 15.06.2027».</summary>
    public static string Period(SiteNotice notice, IStringLocalizer<DiveSiteStrings> l) => notice switch
    {
        { RecursYearly: true, Start: { } start, ValidUntil: { } until } => l["Notice_Yearly", DayMonth(start), DayMonth(until)],
        { Start: { } start, ValidUntil: { } until } => l["Notice_Period", ForecastFormat.Date(start), ForecastFormat.Date(until)],
        { Start: { } start } => l["Notice_From", ForecastFormat.Date(start)],
        _ => "",
    };

    private static string DayMonth(DateOnly date) => date.ToString("dd.MM.", System.Globalization.CultureInfo.InvariantCulture);
}
