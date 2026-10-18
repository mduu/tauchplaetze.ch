namespace Tauchplaetze.Core.Features.Editorial;

internal static class Proposals
{
    /// <summary>Höchstens so viele offene Vorschläge pro Konto (`cms.md`, «Schutz gegen Missbrauch»).</summary>
    public const int MaxPending = 20;

    /// <summary>
    /// Felder, die nur Moderation und Admin ändern: Slug, Prognose aus, Satellitenpunkt, BAFU-Station.
    /// Bei Mitwirkenden gelten die Werte der aktuellen Version.
    /// </summary>
    public static DiveSiteContent ProtectModeratorFields(DiveSiteContent proposed, DiveSiteContent current) => proposed with
    {
        Slug = current.Slug,
        ForecastDisabled = current.ForecastDisabled,
        SatelliteLatitude = current.SatelliteLatitude,
        SatelliteLongitude = current.SatelliteLongitude,
        InflowStationOverride = current.InflowStationOverride,
    };
}
