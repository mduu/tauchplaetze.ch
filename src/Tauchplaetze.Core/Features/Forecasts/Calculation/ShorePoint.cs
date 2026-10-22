namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>Wahl des Sentinel-2-Werts in Ufernähe (`forecast.md`, «Klarheit»): zuerst nah am Ufer, weiter draussen nur als Rückfall.</summary>
internal static class ShorePoint
{
    /// <summary>Distanz des ersten Punkts vom Platz, in Richtung Seemitte. Viele Plätze liegen im Bereich bis 50 m vom Ufer.</summary>
    public const double NearMeters = 50;

    /// <summary>Distanz des Rückfalls, wenn bei <see cref="NearMeters"/> kein Wert oder zu wenige Pixel kommen.</summary>
    public const double FarMeters = 100;

    /// <summary>
    /// Drehungen in Grad für weitere Punkte bei <see cref="FarMeters"/>, wenn 50 und 100 m keinen brauchbaren Wert liefern.
    /// Führt die Richtung zur Seemitte dem Ufer entlang, liegen beide Punkte auf maskierten Uferpixeln (Terlinden, 22.10.2026).
    /// </summary>
    public static readonly double[] FallbackTurns = [45, -45, 90, -90];

    /// <summary>Mindestzahl gültiger Pixel für den neuesten Wert.</summary>
    public const int MinPixels = 3;

    /// <summary>
    /// Braucht es den Rückfall? Ja, ohne Wert, mit weniger als <paramref name="minPixels"/> Pixeln
    /// oder wenn der Wert älter als <paramref name="freshDays"/> Tage ist.
    /// </summary>
    public static bool NeedsFallback(SatelliteObservation? near, DateOnly today, int minPixels, int freshDays) =>
        near is null || near.PixelCount < minPixels || today.DayNumber - near.Date.DayNumber > freshDays;

    /// <summary>
    /// Zuerst der jüngere Wert. Bei gleichem Datum der nahe, ausser er hat zu wenige Pixel und der ferne genug.
    /// Fehlt einer, gilt der andere.
    /// </summary>
    public static SatelliteObservation? Pick(SatelliteObservation? near, SatelliteObservation? far, int minPixels) => (near, far) switch
    {
        (null, _) => far,
        (_, null) => near,
        _ when near.Date != far.Date => near.Date > far.Date ? near : far,
        _ when near.PixelCount < minPixels && far.PixelCount >= minPixels => far,
        _ => near,
    };
}
