namespace Tauchplaetze.Core.Features.WaterBodies;

/// <summary>
/// Ab welcher Höhe ein See als Bergsee gekennzeichnet wird (`ux.md`, «Startseite»).
/// Die Schweizer Tauchtabellen (Bühlmann) haben ab 700 m ü. M. eine eigene Höhenstufe.
/// </summary>
public static class MountainLakes
{
    public const int FromMetres = 700;

    /// <summary>Die Höhe, wenn das Gewässer ein Bergsee ist, sonst <c>null</c>. Flüsse sind nie Bergseen.</summary>
    public static int? Elevation(WaterBodyType type, int? elevation) =>
        type == WaterBodyType.Lake && elevation >= FromMetres ? elevation : null;
}
