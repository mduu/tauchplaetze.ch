using System.Globalization;

namespace Tauchplaetze.Web.Features.Reports;

/// <summary>Zahlen einer Meldung im Schweizer Format: Meter mit höchstens einer Dezimale, °C und Index mit einer Dezimale.</summary>
internal static class ReportFormat
{
    public static string Metres(decimal metres) => metres.ToString("0.#", CultureInfo.CurrentCulture);

    public static string Celsius(decimal celsius) => celsius.ToString("0.0", CultureInfo.CurrentCulture);

    public static string Index(decimal index) => index.ToString("0.0", CultureInfo.CurrentCulture);

    public static string Date(DateOnly date) => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    public static string IsoDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
