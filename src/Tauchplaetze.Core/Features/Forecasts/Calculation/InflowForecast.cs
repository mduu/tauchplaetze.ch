using Tauchplaetze.Core.CrossCutting.Time;

namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>Vorhergesagtes Stundenmittel des Abflusses einer Station, Beginn der Stunde in UTC.</summary>
internal record ForecastHour(string StationId, DateTimeOffset Hour, double Discharge);

/// <summary>Der Abfluss der Zuflüsse an künftigen Tagen aus der BAFU-Vorhersage (`forecast.md`, `rules-1.4`).</summary>
internal static class InflowForecast
{
    /// <summary>So viele Stunden eines Tages braucht das Tagesmittel. Ein Rest am Ende der Vorhersage zählt nicht.</summary>
    public const int MinHoursPerDay = 12;

    /// <summary>
    /// Pro Schweizer Kalendertag: Tagesmittel der Vorhersage geteilt durch den Median der Messwerte der letzten 30 Tage.
    /// Bei mehreren Zuflüssen zählt der höchste Quotient. Tage ohne genug Stunden fehlen.
    /// </summary>
    public static IReadOnlyDictionary<DateOnly, double> Ratios(IReadOnlyDictionary<string, double> medians, IEnumerable<ForecastHour> hours) =>
        hours
            .Where(h => medians.TryGetValue(h.StationId, out var median) && median > 0)
            .GroupBy(h => (h.StationId, Day: SwissTime.DayOf(h.Hour)))
            .Where(g => g.Count() >= MinHoursPerDay)
            .Select(g => (g.Key.Day, Ratio: Math.Round(g.Average(h => h.Discharge) / medians[g.Key.StationId], 2)))
            .GroupBy(r => r.Day)
            .ToDictionary(g => g.Key, g => g.Max(r => r.Ratio));
}
