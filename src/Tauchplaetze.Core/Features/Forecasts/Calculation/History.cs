namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>Glättung und Median für den Verlauf des Index (`forecast.md`, «Verlauf»).</summary>
internal static class History
{
    /// <summary>Tage vor und nach einem Tag im gleitenden Mittel: 3 + 1 + 3 = 7 Tage.</summary>
    public const int HalfWindow = 3;

    /// <summary>
    /// Das gleitende 7-Tage-Mittel pro Tag. Es zählt nur Tage mit Wert im Fenster (Kalendertage, nicht Listeneinträge).
    /// Am Rand und bei Lücken mittelt es über weniger Tage. <c>null</c>, wenn im Fenster kein Wert liegt.
    /// </summary>
    public static IReadOnlyList<decimal?> Smooth(IReadOnlyList<(DateOnly Date, decimal? Value)> days) =>
        days.Select(day =>
            {
                var window = days
                    .Where(d => d.Value is not null && Math.Abs(d.Date.DayNumber - day.Date.DayNumber) <= HalfWindow)
                    .Select(d => d.Value!.Value)
                    .ToList();
                return window.Count > 0 ? Math.Round(window.Average(), 1) : (decimal?)null;
            })
            .ToList();

    public static decimal Median(IReadOnlyCollection<decimal> values)
    {
        var sorted = values.Order().ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : Math.Round((sorted[middle - 1] + sorted[middle]) / 2, 1);
    }
}
