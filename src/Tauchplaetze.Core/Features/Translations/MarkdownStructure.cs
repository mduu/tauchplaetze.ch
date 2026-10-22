using System.Text.RegularExpressions;

namespace Tauchplaetze.Core.Features.Translations;

/// <summary>
/// Vergleicht das einfache Markdown der Texte (`datastructure.md`): Zwischentitel, Listen, Absätze und Fett.
/// Eine Übersetzung muss dieselbe Struktur haben wie die Quelle.
/// </summary>
internal static partial class MarkdownStructure
{
    public static bool Matches(string? source, string? translation) => Of(source) == Of(translation);

    private static string Of(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var blocks = new List<string>();
        var previous = "";
        foreach (var line in text.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()))
        {
            var kind = KindOf(line);
            if (kind != previous || kind is not ("p" or ""))
            {
                blocks.Add(kind);
            }

            previous = kind;
        }

        var bold = text.Split("**").Length - 1;
        return $"{string.Join('|', blocks).Trim('|')}#{bold}";
    }

    private static string KindOf(string line) => line switch
    {
        "" => "",
        _ when Heading().Match(line) is { Success: true } heading => $"h{heading.Groups[1].Length}",
        _ when Bullet().IsMatch(line) => "ul",
        _ when Numbered().IsMatch(line) => "ol",
        _ => "p",
    };

    [GeneratedRegex(@"^(#{1,3})\s+\S")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^[-*]\s+\S")]
    private static partial Regex Bullet();

    [GeneratedRegex(@"^\d{1,3}[.)]\s+\S")]
    private static partial Regex Numbered();
}
