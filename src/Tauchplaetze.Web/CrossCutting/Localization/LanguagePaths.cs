namespace Tauchplaetze.Web.CrossCutting.Localization;

internal static class LanguagePaths
{
    /// <summary>Dieselbe Seite in einer anderen Sprache. Ohne Sprachpräfix führt der Link auf die Startseite der Sprache.</summary>
    /// <remarks>Das Segment nach der Sprache wird übersetzt (`/de/tauchplaetze/x` → `/fr/sites-de-plongee/x`). Der Slug bleibt.</remarks>
    public static string Switch(string relativePath, string language)
    {
        var path = relativePath.Split('?', '#')[0];
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0 || !Languages.IsSupported(segments[0]))
        {
            return $"/{language}";
        }

        var rest = segments[1..].Select((segment, index) => index % 2 == 0 ? PublicRoutes.TranslateSection(segment, language) : segment);

        return "/" + string.Join('/', [language, .. rest]);
    }
}
