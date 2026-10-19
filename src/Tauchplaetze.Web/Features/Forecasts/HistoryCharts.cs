using System.Globalization;
using Tauchplaetze.Core.Features.Forecasts;

namespace Tauchplaetze.Web.Features.Forecasts;

/// <summary>Bildet den Verlauf auf die Achsen der Diagramme ab (`design-system.md`, «Diagramme»).</summary>
internal static class HistoryCharts
{
    /// <summary>Kürzer zeigt das Diagramm mindestens 7 Tage, damit zwei Tage nicht die ganze Breite füllen.</summary>
    public const int MinimumDays = 7;

    /// <summary>Unter so vielen Tagen steht der Hinweis, dass sich der Verlauf noch füllt.</summary>
    public const int FillingBelowDays = 14;

    /// <summary>Die Zeitachse eines Platzes: vom ersten Tag (mindestens 30 Tage zurück) bis heute.</summary>
    public record Range(DateOnly From, DateOnly To)
    {
        public double X(DateOnly date) => To == From ? 1 : (double)(date.DayNumber - From.DayNumber) / (To.DayNumber - From.DayNumber);

        /// <summary>Eine Lücke von mehr als 7 Tagen bricht die Linie ab.</summary>
        public double MaxGap => 7.0 / Math.Max(1, To.DayNumber - From.DayNumber);
    }

    public static Range RangeOf(IReadOnlyList<HistoryDay> days, DateOnly today)
    {
        var from = days.Count > 0 ? days[0].Date : today;
        return new Range(from < today.AddDays(1 - MinimumDays) ? from : today.AddDays(1 - MinimumDays), today);
    }

    /// <summary>Über zwei Monate eine Beschriftung pro Monat, sonst eine pro Woche (dd.MM.).</summary>
    public static IReadOnlyList<ChartTick> Ticks(Range range)
    {
        var span = range.To.DayNumber - range.From.DayNumber;
        if (span > 60)
        {
            var month = new DateOnly(range.From.Year, range.From.Month, 1).AddMonths(1);
            var ticks = new List<ChartTick>();
            for (; month <= range.To; month = month.AddMonths(1))
            {
                ticks.Add(new ChartTick(range.X(month), MonthName(month.Month)));
            }

            return ticks;
        }

        return Enumerable.Range(0, span / 7 + 1)
            .Select(week => range.To.AddDays(-7 * week))
            .Where(date => date >= range.From)
            .Reverse()
            .Select(date => new ChartTick(range.X(date), date.ToString("dd.MM.", CultureInfo.InvariantCulture)))
            .ToList();
    }

    /// <summary>Die Sicht-Achse reicht mindestens bis 10 m und sonst bis zum nächsten vollen Meter über dem höchsten Wert.</summary>
    public static double VisibilityMax(IEnumerable<decimal?> depths) =>
        Math.Max(10, Math.Ceiling((double)(depths.Max() ?? 0)));

    /// <summary>Jahr über Jahr: Tag im Jahr auf 0 bis 1, Schaltjahre gleich lang wie die anderen.</summary>
    public static double DayOfYear(DateOnly date) => (Math.Min(date.DayOfYear, 365) - 1) / 364.0;

    public static IReadOnlyList<ChartTick> MonthTicks() =>
        [.. Enumerable.Range(1, 12).Select(month => new ChartTick(DayOfYear(new DateOnly(2001, month, 1)), MonthName(month)))];

    public static double? Algae(AlgaeLevel level) => level switch
    {
        AlgaeLevel.Low => 1,
        AlgaeLevel.Medium => 2,
        AlgaeLevel.High => 3,
        _ => null,
    };

    private static string MonthName(int month) =>
        CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedMonthNames[month - 1].TrimEnd('.');
}
