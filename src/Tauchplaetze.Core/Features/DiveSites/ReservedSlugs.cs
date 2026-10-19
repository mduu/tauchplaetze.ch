namespace Tauchplaetze.Core.Features.DiveSites;

/// <summary>Segmente der Website neben den Slugs der Plätze (`/{sprache}/{plätze}/neu`, `seo.md`). Ein Platz bekommt sie nie als Slug.</summary>
public static class ReservedSlugs
{
    public static readonly IReadOnlySet<string> All = new HashSet<string> { "neu", "new", "nouveau", "nuovo" };

    /// <summary>Der Slug selbst oder, wenn er reserviert ist, mit `-2`.</summary>
    public static string Avoid(string slug) => All.Contains(slug) ? $"{slug}-2" : slug;
}
