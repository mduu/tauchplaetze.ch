using System.Globalization;
using System.Text;

namespace Tauchplaetze.Core.CrossCutting.Text;

/// <summary>Slugs aus dem deutschen Namen, ohne Umlaute (`datastructure.md`, Grundregeln).</summary>
internal static class Slugs
{
    private static readonly Dictionary<char, string> Umlauts = new()
    {
        ['ä'] = "ae",
        ['ö'] = "oe",
        ['ü'] = "ue",
        ['ß'] = "ss",
    };

    /// <summary>Segmente der Website neben den Slugs der Plätze (`/{sprache}/{plätze}/neu`). Ein Platz bekommt sie nie als Slug.</summary>
    public static readonly IReadOnlySet<string> Reserved = new HashSet<string> { "neu", "new", "nouveau", "nuovo" };

    public static string From(string text)
    {
        var slug = new StringBuilder();

        foreach (var character in WithoutDiacritics(ReplaceUmlauts(text.ToLowerInvariant())))
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                slug.Append(character);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        return slug.ToString().TrimEnd('-');
    }

    private static string ReplaceUmlauts(string text) =>
        string.Concat(text.Select(c => Umlauts.TryGetValue(c, out var replacement) ? replacement : c.ToString()));

    private static string WithoutDiacritics(string text) =>
        string.Concat(text.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));
}
