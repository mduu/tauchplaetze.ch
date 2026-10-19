namespace Tauchplaetze.Core.Features.Editorial;

internal static class Proposals
{
    /// <summary>Höchstens so viele offene Vorschläge pro Konto (`cms.md`, «Schutz gegen Missbrauch»).</summary>
    public const int MaxPending = 20;

    /// <summary>
    /// Felder, die nur Moderation und Admin ändern: Slug, Gewässer, Prognose aus, Satellitenpunkt, BAFU-Station, Schwellen des Abflusses.
    /// Bei Mitwirkenden gelten die Werte der aktuellen Version.
    /// </summary>
    public static DiveSiteContent ProtectModeratorFields(DiveSiteContent proposed, DiveSiteContent current) => proposed with
    {
        Slug = current.Slug,
        WaterBodyId = current.WaterBodyId,
        ForecastDisabled = current.ForecastDisabled,
        SatelliteLatitude = current.SatelliteLatitude,
        SatelliteLongitude = current.SatelliteLongitude,
        InflowStationOverride = current.InflowStationOverride,
        Discharge = current.Discharge,
    };

    /// <summary>Ein Text, den jemand ändert, ist nicht mehr automatisch übersetzt (`i18n.md`, «Maschinelle Übersetzung»).</summary>
    public static DiveSiteContent ClearMachineTranslationOfEditedTexts(DiveSiteContent proposed, DiveSiteContent current) => proposed with
    {
        Translations = [.. proposed.Translations.Select(text => text.IsMachineTranslated && IsEdited(text, current.TextIn(text.Culture)) ? text with { IsMachineTranslated = false } : text)],
    };

    private static bool IsEdited(DiveSiteContentTranslation text, DiveSiteContentTranslation? before) =>
        before is null
        || Differs(text.UnderwaterDescription, before.UnderwaterDescription)
        || Differs(text.SurroundingsInfo, before.SurroundingsInfo)
        || Differs(text.ApproachInfo, before.ApproachInfo)
        || Differs(text.HazardsInfo, before.HazardsInfo)
        || Differs(text.FacilitiesInfo, before.FacilitiesInfo)
        || Differs(text.DivingBanNote, before.DivingBanNote);

    private static bool Differs(string? a, string? b) => (string.IsNullOrWhiteSpace(a) ? null : a.Trim()) != (string.IsNullOrWhiteSpace(b) ? null : b.Trim());
}
