namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>Einordnung des Seepegels gegen den üblichen Bereich des Kalendertags (`forecast.md`, «Seepegel»).</summary>
internal static class LakeLevelRules
{
    /// <summary>Tiefer als das 5. Perzentil ist sehr tief, höher als das 95. Perzentil sehr hoch. Dazwischen <c>null</c>: kein Hinweis.</summary>
    public static LakeLevelDeviation? DeviationOf(decimal level, decimal low, decimal high) =>
        level < low ? LakeLevelDeviation.VeryLow
        : level > high ? LakeLevelDeviation.VeryHigh
        : null;
}
