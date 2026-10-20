using Tauchplaetze.Core.Features.Forecasts.Sources.Hydro;

namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>Einordnung und Trend des Abflusses an Flussplätzen (`forecast.md`, «Abfluss»). Die Einordnung fliesst nicht in den Index.</summary>
internal static class DischargeRules
{
    /// <summary>Faktoren auf den Median der letzten 30 Tage, ab denen der Abfluss gering, mittel und stark ist.</summary>
    public static readonly (decimal Low, decimal Medium, decimal Strong) MedianFactors = (0.5m, 1m, 2m);

    /// <summary>
    /// Untergrenze des Medians in m³/s. Ein kleiner Bach mit fast keinem Wasser ergäbe sonst unsinnige Quotienten (`rules-1.5`).
    /// </summary>
    public const decimal MinMedian = 1m;

    /// <summary>Innerhalb dieses Anteils des Werts von vor 3 Stunden bleibt der Abfluss gleich.</summary>
    public const decimal SteadyWithin = 0.05m;

    /// <summary>Die Schwellen des Platzes, sonst die Schwellen aus dem Median. <c>null</c> ohne beides.</summary>
    public static DischargeThresholds? ThresholdsFor(DischargeThresholds? site, decimal? median) =>
        site ?? (median is { } m and > 0
            ? Thresholds(Math.Max(m, MinMedian))
            : null);

    private static DischargeThresholds Thresholds(decimal median) =>
        new(median * MedianFactors.Low, median * MedianFactors.Medium, median * MedianFactors.Strong);

    public static DischargeLevel LevelOf(decimal discharge, DischargeThresholds thresholds) =>
        discharge < thresholds.LowFrom ? DischargeLevel.VeryLow
        : discharge < thresholds.MediumFrom ? DischargeLevel.Low
        : discharge < thresholds.StrongFrom ? DischargeLevel.Medium
        : DischargeLevel.Strong;

    public static DischargeTrend TrendOf(decimal discharge, decimal before)
    {
        var change = discharge - before;
        if (Math.Abs(change) <= Math.Abs(before) * SteadyWithin)
        {
            return DischargeTrend.Steady;
        }

        return change > 0 ? DischargeTrend.Rising : DischargeTrend.Falling;
    }

    /// <summary>Mittel pro Stunde (UTC), sortiert. Die laufende Stunde hat das Mittel der Werte bis jetzt.</summary>
    public static IReadOnlyList<(DateTimeOffset Hour, decimal Discharge)> HourlyMeans(IEnumerable<HydroValue> values) =>
        values
            .GroupBy(v => StartOfHour(v.Time))
            .OrderBy(g => g.Key)
            .Select(g => (g.Key, Math.Round((decimal)g.Average(v => v.Value), 2)))
            .ToList();

    /// <summary>Der Wert, der am nächsten an <paramref name="time"/> liegt, höchstens 30 Minuten daneben.</summary>
    public static HydroValue? Nearest(IReadOnlyList<HydroValue> values, DateTimeOffset time) =>
        values
            .Where(v => (v.Time - time).Duration() <= TimeSpan.FromMinutes(30))
            .MinBy(v => (v.Time - time).Duration());

    public static decimal? Median(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var sorted = values.Order().ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    public static DateTimeOffset StartOfHour(DateTimeOffset time)
    {
        var utc = time.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
    }
}
