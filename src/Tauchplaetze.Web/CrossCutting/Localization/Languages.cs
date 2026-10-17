using System.Globalization;
using Microsoft.Net.Http.Headers;

namespace Tauchplaetze.Web.CrossCutting.Localization;

/// <summary>Die vier Sprachen der Oberfläche und ihre Kulturen (`i18n.md`).</summary>
internal static class Languages
{
    public const string Default = "de";

    public static readonly IReadOnlyList<string> All = ["de", "en", "fr", "it"];

    public static readonly IReadOnlyList<CultureInfo> Cultures = All.Select(CultureOf).ToList();

    public static bool IsSupported(string? language) => language is not null && All.Contains(language);

    public static CultureInfo CultureOf(string language) => CultureInfo.GetCultureInfo(language switch
    {
        "en" => "en-GB",
        "fr" => "fr-CH",
        "it" => "it-CH",
        _ => "de-CH",
    });

    public static string FromAcceptLanguage(IEnumerable<StringWithQualityHeaderValue> header) =>
        header
            .Where(h => h.Quality is null or > 0)
            .OrderByDescending(h => h.Quality ?? 1)
            .Select(h => LanguageOf(h.Value.Value))
            .FirstOrDefault(IsSupported) ?? Default;

    private static string? LanguageOf(string? tag) =>
        tag?.Split('-')[0].ToLowerInvariant();
}
