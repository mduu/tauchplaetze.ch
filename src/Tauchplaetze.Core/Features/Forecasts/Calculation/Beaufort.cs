namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

internal static class Beaufort
{
    /// <summary>Obergrenzen in km/h für 0 bis 11 Bft (WMO).</summary>
    private static readonly double[] UpperLimitsKmh = [1, 5.5, 11.5, 19.5, 28.5, 38.5, 49.5, 61.5, 74.5, 88.5, 102.5, 117.5];

    public static int FromKmh(double speedKmh)
    {
        var index = Array.FindIndex(UpperLimitsKmh, limit => speedKmh < limit);
        return index < 0 ? 12 : index;
    }
}
