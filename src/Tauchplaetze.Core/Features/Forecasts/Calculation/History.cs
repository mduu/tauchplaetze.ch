namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>Glättung und Median für den Verlauf des Index (`forecast.md`, «Verlauf»).</summary>
internal static class History
{
    /// <summary>Tage vor und nach einem Tag im gleitenden Mittel: 3 + 1 + 3 = 7 Tage.</summary>
    public const int HalfWindow = 3;

    /// <summary>
    /// Das gleitende 7-Tage-Mittel pro Tag. Es zählt nur Tage mit Wert im Fenster (Kalendertage, nicht Listeneinträge).
    /// Am Rand und bei Lücken mittelt es über weniger Tage. <c>null</c>, wenn im Fenster kein Wert liegt.
    /// Die Tage sind nach Datum aufsteigend sortiert. Das Fenster wandert mit, so bleibt die Rechnung linear.
    /// </summary>
    public static IReadOnlyList<decimal?> Smooth(IReadOnlyList<(DateOnly Date, decimal? Value)> days)
    {
        var sums = new decimal[days.Count + 1];
        var counts = new int[days.Count + 1];
        for (var i = 0; i < days.Count; i++)
        {
            sums[i + 1] = sums[i] + (days[i].Value ?? 0);
            counts[i + 1] = counts[i] + (days[i].Value is null ? 0 : 1);
        }

        var result = new decimal?[days.Count];
        var (first, last) = (0, 0);
        for (var i = 0; i < days.Count; i++)
        {
            var day = days[i].Date.DayNumber;
            while (days[first].Date.DayNumber < day - HalfWindow)
            {
                first++;
            }

            while (last < days.Count && days[last].Date.DayNumber <= day + HalfWindow)
            {
                last++;
            }

            var count = counts[last] - counts[first];
            result[i] = count > 0 ? Math.Round((sums[last] - sums[first]) / count, 1) : null;
        }

        return result;
    }

    public static decimal Median(IReadOnlyCollection<decimal> values)
    {
        var sorted = values.Order().ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : Math.Round((sorted[middle - 1] + sorted[middle]) / 2, 1);
    }
}
