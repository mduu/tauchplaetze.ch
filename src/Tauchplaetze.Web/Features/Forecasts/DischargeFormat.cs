using System.Globalization;

namespace Tauchplaetze.Web.Features.Forecasts;

/// <summary>Formate des Abflusses: ab 100 m³/s ganze Zahlen, darunter eine Dezimale, unter 1 m³/s zwei.</summary>
internal static class DischargeFormat
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    public static DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone).DateTime);

    public static string Amount(decimal value) => value.ToString(Pattern(value), CultureInfo.CurrentCulture);

    /// <summary>Mit Vorzeichen, zum Beispiel «+14» oder «−3,5».</summary>
    public static string Change(decimal value)
    {
        var pattern = Pattern(value);
        return value.ToString($"+{pattern};−{pattern};0", CultureInfo.CurrentCulture);
    }

    /// <summary>Uhrzeit in Lokalzeit, mit Datum, wenn der Wert nicht von heute ist.</summary>
    public static string Time(DateTimeOffset time)
    {
        var local = TimeZoneInfo.ConvertTime(time, Zone);
        return local.ToString(DateOnly.FromDateTime(local.DateTime) == Today() ? "HH:mm" : "dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
    }

    private static string Pattern(decimal value) => Math.Abs(value) switch
    {
        >= 100 => "#,##0",
        >= 1 => "0.0",
        _ => "0.00",
    };
}
