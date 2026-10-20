namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>Ein Zufluss mit seinem Quotienten «Abfluss / Median» und seinem Gewicht am Platz.</summary>
internal record WeightedInflow(double Ratio, double Weight);

/// <summary>
/// Wie stark ein Zufluss an einem Platz zählt (`forecast.md`, `rules-1.5`): voll bis 5 km von der Mündung,
/// dann linear weniger, ab 15 km gar nicht. Ein Hochwasser des Ticino trübt Magadino, nicht Brissago.
/// </summary>
internal static class InflowWeights
{
    public const double FullUpToKm = 5;

    public const double NoneFromKm = 15;

    /// <summary>Gewicht 0 bis 1 nach der Distanz zwischen Platz und Mündung. Ohne bekannte Mündung zählt der Zufluss voll.</summary>
    public static double Weight(double? distanceKm) => distanceKm switch
    {
        null or <= FullUpToKm => 1,
        >= NoneFromKm => 0,
        { } km => (NoneFromKm - km) / (NoneFromKm - FullUpToKm),
    };

    /// <summary>
    /// Der Quotient, der am Platz zählt: <c>1 + Gewicht × (Quotient − 1)</c>, bei mehreren Zuflüssen der höchste.
    /// <c>null</c>, wenn kein Zufluss den Platz erreicht.
    /// </summary>
    public static double? Effective(IEnumerable<WeightedInflow> inflows)
    {
        var reaching = inflows.Where(i => i.Weight > 0).ToList();
        return reaching.Count > 0 ? Math.Round(reaching.Max(i => 1 + i.Weight * (i.Ratio - 1)), 2) : null;
    }
}
