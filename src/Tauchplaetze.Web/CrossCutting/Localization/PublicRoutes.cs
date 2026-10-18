namespace Tauchplaetze.Web.CrossCutting.Localization;

/// <summary>Übersetzte Pfadsegmente der öffentlichen Seiten (`i18n.md`, «URLs und Kultur»).</summary>
internal static class PublicRoutes
{
    public static readonly IReadOnlyDictionary<string, string> DiveSites = new Dictionary<string, string>
    {
        ["de"] = "tauchplaetze",
        ["en"] = "dive-sites",
        ["fr"] = "sites-de-plongee",
        ["it"] = "siti-di-immersione",
    };

    public static readonly IReadOnlyDictionary<string, string> WaterBodies = new Dictionary<string, string>
    {
        ["de"] = "gewaesser",
        ["en"] = "waters",
        ["fr"] = "eaux",
        ["it"] = "acque",
    };

    public static readonly IReadOnlyDictionary<string, string> Forecast = new Dictionary<string, string>
    {
        ["de"] = "prognose",
        ["en"] = "forecast",
        ["fr"] = "prevision",
        ["it"] = "previsione",
    };

    public static readonly IReadOnlyDictionary<string, string> Versions = new Dictionary<string, string>
    {
        ["de"] = "versionen",
        ["en"] = "versions",
        ["fr"] = "versions",
        ["it"] = "versioni",
    };

    private static readonly IReadOnlyList<IReadOnlyDictionary<string, string>> Sections = [DiveSites, WaterBodies, Forecast, Versions];

    public static string DiveSite(string language, string slug) => $"/{language}/{DiveSites[language]}/{slug}";

    public static string DiveSiteVersions(string language, string slug) => $"{DiveSite(language, slug)}/{Versions[language]}";

    /// <summary>Das Segment <paramref name="segment"/> einer Sprache in einer anderen Sprache. Unbekannte Segmente bleiben gleich.</summary>
    public static string TranslateSection(string segment, string language) =>
        Sections.FirstOrDefault(s => s.Values.Contains(segment)) is { } section ? section[language] : segment;
}
