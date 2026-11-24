namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>Einordnung des Seepegels gegen den üblichen Bereich des Kalendertags (`forecast.md`, «Seepegel»).</summary>
internal static class LakeLevelRules
{
    /// <summary>Ab diesem Abstand zum Median in m gibt es einen Hinweis. An regulierten Seen ist der übliche Bereich schmal, wenige cm ändern am Einstieg nichts.</summary>
    public const decimal MinDistanceFromMedian = 0.30m;

    /// <summary>
    /// Tiefer als das 5. Perzentil ist sehr tief, höher als das 95. Perzentil sehr hoch, beides erst ab <see cref="MinDistanceFromMedian"/> Abstand zum Median.
    /// Sonst <c>null</c>: kein Hinweis.
    /// </summary>
    public static LakeLevelDeviation? DeviationOf(decimal level, decimal low, decimal median, decimal high) =>
        Math.Abs(level - median) < MinDistanceFromMedian ? null
        : level < low ? LakeLevelDeviation.VeryLow
        : level > high ? LakeLevelDeviation.VeryHigh
        : null;
}
