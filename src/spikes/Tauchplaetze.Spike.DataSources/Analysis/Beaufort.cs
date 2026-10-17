namespace Tauchplaetze.Spike.DataSources.Analysis;

/// <summary>
/// Converts wind speed to the Beaufort scale.
/// </summary>
internal static class Beaufort
{
    // Upper limits in km/h for Beaufort 0 to 11 (WMO).
    private static readonly double[] UpperLimitsKmh = [1, 5.5, 11.5, 19.5, 28.5, 38.5, 49.5, 61.5, 74.5, 88.5, 102.5, 117.5];

    public static int FromKmh(double speedKmh)
    {
        for (var beaufort = 0; beaufort < UpperLimitsKmh.Length; beaufort++)
        {
            if (speedKmh < UpperLimitsKmh[beaufort])
            {
                return beaufort;
            }
        }

        return 12;
    }
}
