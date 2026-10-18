using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.Features.Forecasts.Sources.Alplakes;

namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>Das Profil eines Tages und seine Herkunft.</summary>
internal record DayProfile(TemperatureProfile Profile, ProfileSource Source);

/// <summary>Datenstufe und Wahl des Profils (`forecast.md`, «Datenstufe»).</summary>
internal static class DataLevelRule
{
    /// <summary>Höchste Distanz in m zwischen Platz und Gitterpunkt des 3D-Modells. Weiter weg gilt das Profil nicht mehr «am Platz».</summary>
    public const double MaxGridDistance = 500;

    private static readonly TimeSpan ProfileTolerance = TimeSpan.FromHours(3);

    /// <summary>Das Profil um 12 Uhr Lokalzeit: zuerst das 3D-Modell am Platz, sonst das 1D-Modell des Sees.</summary>
    public static DayProfile? ChooseProfile(DateOnly day, DepthTimeSeries? model3d, DepthTimeSeries? model1d)
    {
        var noon = SwissTime.ToUtc(day, 12);

        if (model3d is { DistanceToGridPoint: <= MaxGridDistance } && model3d.ProfileAt(noon, ProfileTolerance) is { Points.Count: > 0 } atSite)
        {
            return new DayProfile(atSite, ProfileSource.Model3d);
        }

        return model1d?.ProfileAt(noon, ProfileTolerance) is { Points.Count: > 0 } lakeWide
            ? new DayProfile(lakeWide, ProfileSource.Model1d)
            : null;
    }

    public static byte For(bool forecastDisabled, ProfileSource? source) => (forecastDisabled, source) switch
    {
        (true, _) => 0,
        (_, ProfileSource.Model3d) => 3,
        (_, ProfileSource.Model1d or ProfileSource.RiverStation or ProfileSource.InSitu) => 2,
        _ => 1,
    };
}
