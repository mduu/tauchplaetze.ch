using System.Text.RegularExpressions;

namespace Tauchplaetze.Core.Features.Editorial;

internal static partial class ContentValidation
{
    public static IReadOnlyList<EditorialError> Errors(DiveSiteContent content, bool waterBodyExists)
    {
        var errors = new List<EditorialError>();
        if (string.IsNullOrWhiteSpace(content.Name) || content.Name.Trim().Length > 120)
        {
            errors.Add(EditorialError.InvalidName);
        }

        if (!waterBodyExists)
        {
            errors.Add(EditorialError.UnknownWaterBody);
        }

        if (!InSwitzerland(content.Latitude, content.Longitude) || content.EntryPointList.Any(e => !InSwitzerland(e.Latitude, e.Longitude)))
        {
            errors.Add(EditorialError.LocationOutsideSwitzerland);
        }

        byte?[] ratings = [content.DifficultyRating, content.WallRating, content.MarineLifeRating, content.WrecksRating, content.CurrentRating, content.SurroundingsRating];
        if (ratings.Any(r => r is < 1 or > 5))
        {
            errors.Add(EditorialError.InvalidRating);
        }

        if (!SlugPattern().IsMatch(content.Slug) || content.Slug.Length > 120)
        {
            errors.Add(EditorialError.InvalidSlug);
        }

        return errors;
    }

    /// <remarks>Grob die Landesgrenze mit etwas Rand, damit Plätze an der Grenze (Bodensee, Genfersee, Rhein bei Basel) gelten.</remarks>
    private static bool InSwitzerland(double latitude, double longitude) => latitude is >= 45.7 and <= 47.9 && longitude is >= 5.8 and <= 10.6;

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}
