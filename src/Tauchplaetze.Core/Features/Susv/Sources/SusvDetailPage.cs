using System.Net;
using System.Text.RegularExpressions;

namespace Tauchplaetze.Core.Features.Susv.Sources;

/// <summary>Liest den Haupttext der Seite eines Eintrags auf susv.ch.</summary>
internal static partial class SusvDetailPage
{
    /// <remarks>
    /// Die Seite ist im Aufbau (Antwort SUSV, 25.11.2026). Der Leser hängt darum an keiner CSS-Klasse:
    /// Er nimmt den Bereich `main` (sonst `body`), entfernt Navigation, Kopf, Fuss, Formulare und Knöpfe
    /// und sammelt die Absätze und Listenpunkte. Ein Absatz, der in einem längeren steckt (Lead und Text), fällt weg.
    /// Gibt es keinen Absatz, ist das Ergebnis <c>null</c>. Dann gilt der bisherige Text oder der Kurztext.
    /// </remarks>
    public static string? MainText(string html, string title)
    {
        var area = MainArea().Match(html) is { Success: true } main ? main.Value
            : Body().Match(html) is { Success: true } body ? body.Value
            : html;
        area = Comments().Replace(area, " ");
        area = Noise().Replace(area, " ");

        var blocks = Blocks().Matches(area)
            .Select(m => Clean(m.Groups["inner"].Value))
            .Where(b => b.Length > 0 && !string.Equals(b, title, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();
        blocks = blocks.Where(b => !blocks.Any(other => other.Length > b.Length && Flat(other).Contains(Flat(b), StringComparison.Ordinal))).ToList();

        return blocks.Count == 0 ? null : SusvRules.Shorten(string.Join("\n\n", blocks), SusvRules.TextLength);
    }

    /// <summary>Zeilenumbrüche aus `br` bleiben, sonst Text ohne HTML, mit einfachem Leerraum.</summary>
    private static string Clean(string inner)
    {
        var text = LineBreak().Replace(inner, "\n");
        text = WebUtility.HtmlDecode(Tag().Replace(text, " "));
        var lines = string.Join("\n", text.Split('\n').Select(SusvRules.OneLine));
        return BlankLines().Replace(lines, "\n\n").Trim('\n');
    }

    private static string Flat(string text) => SusvRules.OneLine(text);

    [GeneratedRegex(@"<main\b.*?</main>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex MainArea();

    [GeneratedRegex(@"<body\b.*?</body>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Body();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex Comments();

    [GeneratedRegex(@"<(script|style|svg|noscript|template|iframe|form|button|select|nav|header|footer|figure)\b.*?</\1\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Noise();

    [GeneratedRegex(@"<(p|li)\b[^>]*>(?<inner>.*?)</\1\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Blocks();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreak();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLines();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tag();
}
