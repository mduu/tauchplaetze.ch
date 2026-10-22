using Tauchplaetze.Core.Features.Forecasts.Sources.Alplakes;

namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>
/// Wahl des Gitterpunkts im 3D-Modell (`forecast.md`, «Profil am Platz»): zuerst am Platz, bei einem zu flachen Gitterpunkt weiter draussen.
/// Am Ufer liegt der nächste Gitterpunkt oft im Flachwasser, das Profil endet dann nach wenigen Metern.
/// </summary>
internal static class ModelPoint
{
    /// <summary>So tief muss das Profil reichen. Die Seite zeigt die Temperatur bis 30 m.</summary>
    public const double MinDepth = 30;

    /// <summary>Distanzen der Rückfälle vom Platz Richtung Seemitte, in m.</summary>
    public static readonly double[] SeawardMeters = [100, 250];

    /// <summary>Braucht es einen Rückfall? Nur mit Antwort: Fehlt sie, fehlt sie weiter draussen auch.</summary>
    public static bool TooShallow(DepthTimeSeries? series) => series is not null && !(series.DeepestValue >= MinDepth);

    /// <summary>Das tiefere Profil. Bei gleicher Tiefe das nähere. Fehlt eines, gilt das andere.</summary>
    public static DepthTimeSeries? Deeper(DepthTimeSeries? near, DepthTimeSeries? far) => (near, far) switch
    {
        (null, _) => far,
        (_, null) => near,
        _ => (far.DeepestValue ?? 0) > (near.DeepestValue ?? 0) ? far : near,
    };

    /// <summary>Die Distanz zum Gitterpunkt zählt ab dem Platz, nicht ab dem Punkt draussen (`DataLevelRule.MaxGridDistance`).</summary>
    public static DepthTimeSeries FromSite(DepthTimeSeries seaward, double meters) =>
        seaward with { DistanceToGridPoint = seaward.DistanceToGridPoint + meters };
}
