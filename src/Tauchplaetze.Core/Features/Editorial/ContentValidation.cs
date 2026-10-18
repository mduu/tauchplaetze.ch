using System.Text.RegularExpressions;
using Tauchplaetze.Core.CrossCutting.Geo;
using Tauchplaetze.Core.Features.DiveSites;

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

        if (content.NoticeList.Any(IsInvalid))
        {
            errors.Add(EditorialError.InvalidNotice);
        }

        if (content.SketchList.Count > SketchRules.MaxFeatures
            || content.SketchList.DistinctBy(f => f.Id).Count() != content.SketchList.Count
            || content.SketchList.Any(f => IsInvalid(f, content)))
        {
            errors.Add(EditorialError.InvalidSketch);
        }

        return errors;
    }

    private static bool IsInvalid(NoticeContent notice) =>
        string.IsNullOrWhiteSpace(notice.Texts.FirstOrDefault(t => t.Culture == "de")?.Text)
        || notice.Texts.Any(t => t.Text?.Trim().Length > NoticeRules.TextLength)
        || notice is { ValidFrom: { } from, ValidUntil: { } until } && until < from;

    private static bool IsInvalid(SketchFeature feature, DiveSiteContent content) =>
        !Enum.IsDefined(feature.Kind)
        || feature.Texts.Any(t => !Cultures.Contains(t.Culture))
        || feature.Kind.ShapeOf() switch
        {
            SketchShape.Point => feature.Points.Count != 1,
            SketchShape.Line => feature.Points.Count is < 2 or > SketchRules.MaxPoints,
            _ => feature.Points.Count is < 3 or > SketchRules.MaxPoints,
        }
        || feature.Points.Any(p => Distance.Km(GeoPoints.Create(content.Latitude, content.Longitude), GeoPoints.Create(p.Latitude, p.Longitude)) * 1000 > SketchRules.MaxDistanceMeters)
        || feature.Depth is < 0 or > SketchRules.MaxDepth
        || feature.Texts.Any(t => t.Label?.Trim().Length > SketchRules.LabelLength);

    /// <remarks>Grob die Landesgrenze mit etwas Rand, damit Plätze an der Grenze (Bodensee, Genfersee, Rhein bei Basel) gelten.</remarks>
    private static bool InSwitzerland(double latitude, double longitude) => latitude is >= 45.7 and <= 47.9 && longitude is >= 5.8 and <= 10.6;

    private static readonly HashSet<string> Cultures = ["de", "en", "fr", "it"];

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}
