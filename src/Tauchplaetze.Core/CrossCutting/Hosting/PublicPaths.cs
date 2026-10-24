namespace Tauchplaetze.Core.CrossCutting.Hosting;

/// <summary>Pfade der öffentlichen Seiten von Platz und Gewässer in den vier Sprachen (`i18n.md`, «URLs und Kultur»). Die Web-App nutzt dieselben Segmente.</summary>
public static class PublicPaths
{
    public static readonly IReadOnlyList<string> Languages = ["de", "en", "fr", "it"];

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

    public static string DiveSite(string language, string slug) => $"/{language}/{DiveSites[language]}/{slug}";

    public static string WaterBody(string language, string slug) => $"/{language}/{WaterBodies[language]}/{slug}";
}
