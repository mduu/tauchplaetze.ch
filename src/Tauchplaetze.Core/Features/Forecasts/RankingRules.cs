namespace Tauchplaetze.Core.Features.Forecasts;

/// <summary>Reihung der Rangliste auf der Startseite (`forecast.md`, «Rangliste»). Der angezeigte Index bleibt unverändert.</summary>
public static class RankingRules
{
    /// <summary>So viele Plätze eines Gewässers stehen in der Rangliste, die weiteren aufklappbar beim zweiten.</summary>
    public const int SitesPerWaterBody = 2;

    /// <summary>Mit gewähltem Ort zieht jeder km Distanz so viel von der Reihung ab (0.1 Punkte pro 10 km).</summary>
    public const decimal PenaltyPerKm = 0.01m;

    /// <summary>Wert für die Reihung: Index, mit Ort abzüglich der Distanz.</summary>
    public static decimal Score(decimal diveIndex, double? distanceKm) =>
        distanceKm is { } km ? diveIndex - PenaltyPerKm * (decimal)km : diveIndex;
}
