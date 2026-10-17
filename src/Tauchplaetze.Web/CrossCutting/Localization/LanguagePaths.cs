namespace Tauchplaetze.Web.CrossCutting.Localization;

internal static class LanguagePaths
{
    /// <summary>Dieselbe Seite in einer anderen Sprache. Ohne Sprachpräfix führt der Link auf die Startseite der Sprache.</summary>
    /// <remarks>Übersetzte Pfadsegmente (`i18n.md`) kommen mit den ersten Seiten dazu, die sie brauchen.</remarks>
    public static string Switch(string relativePath, string language)
    {
        var path = relativePath.Split('?', '#')[0];
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0 || !Languages.IsSupported(segments[0]))
        {
            return $"/{language}";
        }

        return "/" + string.Join('/', [language, .. segments[1..]]);
    }
}
