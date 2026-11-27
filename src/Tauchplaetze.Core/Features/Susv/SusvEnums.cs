namespace Tauchplaetze.Core.Features.Susv;

/// <summary>Thema eines Eintrags des SUSV (`stories/002-susv-import-und-moderation.md`, «Themen der Quelle»). Steht als Text in der Datenbank.</summary>
public enum SusvTopic
{
    /// <summary>Tauchverbote und Einschränkungen.</summary>
    Restriction,

    Defibrillator,

    MobileToilet,

    /// <summary>Abgeschlossenes Förderprojekt, z.B. ein verbesserter Einstieg.</summary>
    SupportProject,

    /// <summary>Inklusions-Tauchplatz: geeignet für Tauchende mit Behinderung.</summary>
    InclusiveSite,
}

/// <summary>Zustand eines Eintrags in der Liste der Prüfung, in dieser Reihenfolge sortiert.</summary>
public enum SusvReviewState
{
    Open,
    Changed,
    Confirmed,
    Ignored,
    Removed,
}

/// <summary>Stand der Prüfung durch die Moderation (ADR-013).</summary>
public enum SusvReviewStatus
{
    /// <summary>Neu, noch nicht geprüft. Unsichtbar.</summary>
    Pending,

    /// <summary>Bestätigt und Plätzen zugeordnet.</summary>
    Confirmed,

    /// <summary>Ohne Bezug zu einem Platz. Unsichtbar.</summary>
    Ignored,
}

/// <summary>Was die Moderation mit einem Eintrag tut.</summary>
public enum SusvReviewAction
{
    /// <summary>Bestätigen oder eine Bestätigung ändern: Plätze, bei einer Einschränkung Art und Zeitraum.</summary>
    Confirm,

    /// <summary>Ohne Bezug zu einem Platz.</summary>
    Ignore,

    /// <summary>«Ignorieren» zurücknehmen. Der Eintrag ist wieder offen.</summary>
    Reopen,

    /// <summary>Die Änderung der Quelle ist gesehen. Plätze, Art und Zeitraum bleiben.</summary>
    MarkSeen,
}

public enum SusvReviewError
{
    NotAllowed,

    Blocked,

    NotFound,

    /// <summary>Bestätigen braucht mindestens einen veröffentlichten Platz.</summary>
    NoSites,

    /// <summary>Ein gewählter Platz fehlt oder ist nicht veröffentlicht.</summary>
    UnknownSite,

    /// <summary>Eine Einschränkung braucht eine Art.</summary>
    KindRequired,

    /// <summary>Art und Zeitraum gibt es nur bei einer Einschränkung.</summary>
    KindNotAllowed,

    /// <summary>«Jedes Jahr» braucht «gültig ab» und «gültig bis».</summary>
    YearlyNeedsDates,

    /// <summary>«Gültig bis» liegt vor «gültig ab».</summary>
    InvalidPeriod,

    /// <summary>«Gesehen» gibt es nur bei einem bestätigten, geänderten Eintrag.</summary>
    NotChanged,

    /// <summary>Der Abgleich läuft gerade. Er schreibt dieselben Einträge, darum wartet die Prüfung (Lock `susv`).</summary>
    Busy,
}
