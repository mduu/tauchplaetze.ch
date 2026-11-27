using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Susv;

/// <summary>Regeln des Imports und der Prüfung (`stories/002-susv-import-und-moderation.md`).</summary>
internal static partial class SusvRules
{
    public const int TitleLength = 200;

    public const int TeaserLength = 300;

    public const int TextLength = 4000;

    public const int UrlLength = 500;

    /// <summary>Nummern der Themen in der Schnittstelle (`mapFilter`).</summary>
    public static readonly IReadOnlyDictionary<SusvTopic, int> Filters = new Dictionary<SusvTopic, int>
    {
        [SusvTopic.Restriction] = 15547,
        [SusvTopic.Defibrillator] = 12630,
        [SusvTopic.MobileToilet] = 12629,
        [SusvTopic.SupportProject] = 12628,
        [SusvTopic.InclusiveSite] = 70259,
    };

    /// <summary>Gepflegte Sprachen der Quelle. Deutsch ist Pflicht.</summary>
    public static readonly string[] Cultures = ["de", "fr", "it"];

    /// <summary>Fehlt ein Eintrag so viele Läufe in Folge, gilt er als entfernt.</summary>
    public const int MissedRunsUntilRemoved = 2;

    /// <summary>So lange steht ein entfernter Eintrag noch in der Liste der Moderation.</summary>
    public static readonly TimeSpan RemovedRetention = TimeSpan.FromDays(30);

    /// <summary>Umkreis für die vorgeschlagenen Plätze.</summary>
    public const double SuggestionRadiusMeters = 500;

    /// <summary>
    /// Fehlt in einem Lauf mehr als dieser Anteil der bekannten Einträge eines Themas, ist die Antwort verdächtig:
    /// eher ein Umbau der Seite als ein Aufräumen. Dann zählt der Lauf für dieses Thema nicht als «fehlt».
    /// </summary>
    public const double SuspiciousMissingShare = 0.5;

    /// <summary>Unter so vielen bekannten Einträgen greift <see cref="SuspiciousMissingShare"/> nicht.</summary>
    public const int SuspiciousMinimumKnown = 4;

    /// <summary>Auf den Platzseiten sichtbar: bestätigte Einschränkung mit Art, bei der Quelle nicht entfernt (Story 003).</summary>
    public static readonly Expression<Func<SusvEntry, bool>> ShownRestriction = e =>
        e.Topic == SusvTopic.Restriction && e.ReviewStatus == SusvReviewStatus.Confirmed && e.RemovedAt == null && e.NoticeKind != null;

    /// <summary>Auf den Platzseiten sichtbar: bestätigtes Projekt (alle Themen ausser Einschränkung), bei der Quelle nicht entfernt (Story 004).</summary>
    public static readonly Expression<Func<SusvEntry, bool>> ShownProject = e =>
        e.Topic != SusvTopic.Restriction && e.ReviewStatus == SusvReviewStatus.Confirmed && e.RemovedAt == null;

    /// <summary>Höchstlänge des Texts im Banner. Länger: gekürzt am Satzende, dann «Mehr beim SUSV».</summary>
    public const int BannerTextLength = 500;

    /// <summary>
    /// Nur bei einer Einschränkung zeigt die Platzseite den Text des SUSV. Projekte zeigen einen festen Text (`stories/004-susv-projekte-nach-thema.md`).
    /// Ihre Seiten sind zudem oft Platzseiten mit Messwerten (Wassertemperatur, Pegel), die sich täglich ändern (Abruf vom 26.11.2026).
    /// </summary>
    public static bool HasDetailText(SusvTopic topic) => topic == SusvTopic.Restriction;

    /// <summary>Ersetzt Tabulatoren, Zeilenumbrüche und mehrfachen Leerraum durch ein Leerzeichen.</summary>
    public static string OneLine(string text) => Whitespace().Replace(text, " ").Trim();

    /// <summary>Kürzt auf <paramref name="max"/> Zeichen, am Ende des letzten ganzen Satzes. Ohne Satzende hart mit «…».</summary>
    public static string Shorten(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        var cut = text[..max];
        var end = cut.LastIndexOfAny(['.', '!', '?']);
        return end > max / 3 ? cut[..(end + 1)] : cut[..(max - 1)].TrimEnd() + "…";
    }

    /// <summary>
    /// Ein Eintrag gilt in diesem Lauf als «fehlt» nur, wenn das Thema geliefert hat und die Antwort plausibel ist:
    /// nicht leer und nicht der grösste Teil der bekannten Einträge auf einmal weg.
    /// </summary>
    public static bool MayCountMissing(int known, int delivered, int missing) =>
        delivered > 0 && (known < SuspiciousMinimumKnown || missing <= known * SuspiciousMissingShare);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
