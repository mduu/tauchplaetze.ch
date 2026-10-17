namespace Tauchplaetze.Core.Features.WaterBodies;

internal static class WaterBodyNames
{
    public static string In(string culture, string nameDe, string? nameEn, string? nameFr, string? nameIt)
    {
        var localized = culture switch
        {
            "en" => nameEn,
            "fr" => nameFr,
            "it" => nameIt,
            _ => nameDe,
        };

        return string.IsNullOrWhiteSpace(localized) ? nameDe : localized;
    }
}
