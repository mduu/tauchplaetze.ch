using System.Globalization;
using Tauchplaetze.Core.Features.Forecasts;

namespace Tauchplaetze.Web.Features.Forecasts;

/// <summary>Schweizer Formate der Prognose (`i18n.md`, «Formate»): °C mit einer Dezimale, ganze Meter.</summary>
internal static class ForecastFormat
{
    public static string Number(decimal value) => value.ToString("0.0", CultureInfo.CurrentCulture);

    public static string Meters(decimal value) => Math.Round(value).ToString("0", CultureInfo.CurrentCulture);

    /// <summary>Geschwindigkeit in m/s als ganze cm/s, mindestens «&lt; 1» für sehr schwache Strömung.</summary>
    public static string CentimetresPerSecond(decimal metresPerSecond) =>
        metresPerSecond * 100 < 0.5m ? "< 1" : Math.Round(metresPerSecond * 100).ToString("0", CultureInfo.CurrentCulture);

    /// <summary>Eine der acht Himmelsrichtungen (N, NE, E, SE, S, SW, W, NW) zur Richtung in Grad.</summary>
    public static string Compass(int degrees) =>
        new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" }[(int)Math.Round(((degrees % 360) + 360) % 360 / 45.0) % 8];

    public static string Date(DateOnly date) => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    public static string Weekday(DateOnly date) => CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(date.DayOfWeek).TrimEnd('.');

    /// <summary>Die Indexskala des Design-Systems: ab 8 hoch, ab 7 mittel, sonst tief.</summary>
    public static string IndexBand(decimal? index) => index switch
    {
        null => "none",
        >= 8 => "high",
        >= 7 => "mid",
        _ => "low",
    };

    public static string Headline(ForecastDay day) => day.DiveIndex switch
    {
        null => "Headline_Outlook",
        >= 8 => "Headline_Good",
        >= 6 => "Headline_Fair",
        _ => "Headline_Poor",
    };
}
