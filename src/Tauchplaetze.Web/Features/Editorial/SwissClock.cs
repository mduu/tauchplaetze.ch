using System.Globalization;

namespace Tauchplaetze.Web.Features.Editorial;

/// <summary>Zeitpunkte im CMS in Schweizer Zeit, unabhängig von der Zeitzone des Servers.</summary>
internal static class SwissClock
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    public static string DateTime(DateTimeOffset time) => TimeZoneInfo.ConvertTime(time, Zone).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string Date(DateTimeOffset time) => TimeZoneInfo.ConvertTime(time, Zone).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
}
