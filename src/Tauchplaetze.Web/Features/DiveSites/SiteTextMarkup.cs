using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Tauchplaetze.Web.Features.DiveSites;

/// <summary>Macht aus einem redaktionellen Text sicheres HTML: Absätze, Zeilenumbrüche und, mit <c>markdown</c>, Listen («- », «* ») und Fett (**…**) wie in `datastructure.md`. Alles andere bleibt Text.</summary>
public static partial class SiteTextMarkup
{
    public static string ToHtml(string text, bool markdown)
    {
        var html = new StringBuilder();
        foreach (var block in Blocks(text))
        {
            var lines = block.Split('\n');
            if (markdown && lines.All(IsListItem))
            {
                html.Append("<ul>");
                foreach (var line in lines)
                {
                    html.Append("<li>").Append(Inline(line.TrimStart()[2..].Trim(), markdown)).Append("</li>");
                }

                html.Append("</ul>");
            }
            else
            {
                html.Append("<p>").Append(string.Join("<br>", lines.Select(l => Inline(l.Trim(), markdown)))).Append("</p>");
            }
        }

        return html.ToString();
    }

    private static IEnumerable<string> Blocks(string text) =>
        BlankLine().Split(text.Replace("\r\n", "\n").Trim()).Select(b => b.Trim('\n')).Where(b => b.Trim().Length > 0);

    private static bool IsListItem(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal);
    }

    private static string Inline(string line, bool markdown)
    {
        var encoded = WebUtility.HtmlEncode(line);
        return markdown ? Bold().Replace(encoded, "<strong>$1</strong>") : encoded;
    }

    [GeneratedRegex(@"\n[ \t]*\n")]
    private static partial Regex BlankLine();

    [GeneratedRegex(@"\*\*(\S(?:.*?\S)?)\*\*")]
    private static partial Regex Bold();
}
