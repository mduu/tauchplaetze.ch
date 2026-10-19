namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>Wahl des Sentinel-2-Werts in Ufernähe (`forecast.md`, «Klarheit»): zuerst nah am Ufer, weiter draussen nur als Rückfall.</summary>
internal static class ShorePoint
{
    /// <summary>Distanz des ersten Punkts vom Platz, in Richtung Seemitte. Viele Plätze liegen im Bereich bis 50 m vom Ufer.</summary>
    public const double NearMeters = 50;

    /// <summary>Distanz des Rückfalls, wenn bei <see cref="NearMeters"/> kein Wert oder zu wenige Pixel kommen.</summary>
    public const double FarMeters = 100;

    /// <summary>Mindestzahl gültiger Pixel für den neuesten Wert.</summary>
    public const int MinPixels = 3;

    /// <summary>Braucht es den Rückfall? Ja, ohne Wert oder mit weniger als <paramref name="minPixels"/> Pixeln.</summary>
    public static bool NeedsFallback(SatelliteObservation? near, int minPixels) => near is null || near.PixelCount < minPixels;

    /// <summary>
    /// Der Wert nah am Ufer, wenn er genug Pixel hat. Sonst der Rückfall, wenn er genug Pixel hat.
    /// Haben beide zu wenige, gilt der nahe Wert (sonst der ferne): ein knapper Wert ist besser als keiner.
    /// </summary>
    public static SatelliteObservation? Pick(SatelliteObservation? near, SatelliteObservation? far, int minPixels) =>
        !NeedsFallback(near, minPixels) ? near
        : far is { } enough && enough.PixelCount >= minPixels ? far
        : near ?? far;
}
