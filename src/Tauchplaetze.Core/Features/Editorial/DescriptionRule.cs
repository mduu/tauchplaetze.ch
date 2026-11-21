namespace Tauchplaetze.Core.Features.Editorial;

/// <summary>Wann ein Platz beschrieben ist (`cms.md`, «Erste Beschreibung»).</summary>
internal static class DescriptionRule
{
    /// <summary>
    /// Mindestens ein Text in einer Sprache, nicht maschinell übersetzt: Unter Wasser, Umgebung, Anfahrt, Gefahren oder Infrastruktur.
    /// Bewertungen, Fotos, Einstiege und der Hinweis zum Tauchverbot zählen nicht.
    /// </summary>
    public static bool IsDescribed(DiveSiteContent content) =>
        content.Translations.Any(t => !t.IsMachineTranslated
            && new[] { t.UnderwaterDescription, t.SurroundingsInfo, t.ApproachInfo, t.HazardsInfo, t.FacilitiesInfo }.Any(text => !string.IsNullOrWhiteSpace(text)));
}
