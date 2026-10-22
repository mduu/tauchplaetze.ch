using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Tauchplaetze.Web.Features.DiveSites;

/// <summary>Macht aus einem redaktionellen Text sicheres HTML mit einfachem Markdown wie in `datastructure.md`: Absätze, Zwischentitel («# » bis «### »), Listen («- », «* », «1. »), Fett (**…**) und Kursiv (*…*, _…_). Alles andere bleibt Text.</summary>
public static partial class SiteTextMarkup
{
    public static string ToHtml(string text)
    {
        var html = new StringBuilder();
        string? openTag = null;
        var paragraph = new List<string>();

        foreach (var line in text.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()))
        {
            var heading = Heading().Match(line);
            var bullet = Bullet().Match(line);
            var numbered = Numbered().Match(line);
            var listTag = bullet.Success ? "ul" : numbered.Success ? "ol" : null;

            if (line.Length == 0 || heading.Success || listTag is not null)
            {
                Flush(html, paragraph);
            }

            if (openTag is not null && openTag != listTag)
            {
                html.Append("</").Append(openTag).Append('>');
                openTag = null;
            }

            if (heading.Success)
            {
                var tag = $"h{3 + heading.Groups[1].Length}";
                html.Append('<').Append(tag).Append('>').Append(Inline(heading.Groups[2].Value)).Append("</").Append(tag).Append('>');
            }
            else if (listTag is not null)
            {
                if (openTag is null)
                {
                    openTag = listTag;
                    html.Append(StartTag(listTag, numbered));
                }

                html.Append("<li>").Append(Inline((bullet.Success ? bullet : numbered).Groups["text"].Value)).Append("</li>");
            }
            else if (line.Length > 0)
            {
                paragraph.Add(line);
            }
        }

        Flush(html, paragraph);
        if (openTag is not null)
        {
            html.Append("</").Append(openTag).Append('>');
        }

        return html.ToString();
    }

    private static string StartTag(string listTag, Match numbered) =>
        listTag == "ol" && int.TryParse(numbered.Groups[1].Value, out var start) && start != 1 ? $"<ol start=\"{start}\">" : $"<{listTag}>";

    private static void Flush(StringBuilder html, List<string> paragraph)
    {
        if (paragraph.Count == 0)
        {
            return;
        }

        html.Append("<p>").Append(string.Join("<br>", paragraph.Select(Inline))).Append("</p>");
        paragraph.Clear();
    }

    private static string Inline(string line)
    {
        var encoded = WebUtility.HtmlEncode(line.Trim());
        var bold = Bold().Replace(encoded, "<strong>$1</strong>");
        return Italic().Replace(bold, "<em>$1$2</em>");
    }

    [GeneratedRegex(@"^(#{1,3})\s+(.+?)\s*#*$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^[-*]\s+(?<text>.+)$")]
    private static partial Regex Bullet();

    [GeneratedRegex(@"^(\d{1,3})[.)]\s+(?<text>.+)$")]
    private static partial Regex Numbered();

    [GeneratedRegex(@"\*\*(\S(?:.*?\S)?)\*\*")]
    private static partial Regex Bold();

    [GeneratedRegex(@"(?<![\w*])\*(\S(?:.*?\S)?)\*(?![\w*])|(?<!\w)_(\S(?:.*?\S)?)_(?!\w)")]
    private static partial Regex Italic();
}
