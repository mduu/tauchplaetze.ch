namespace Tauchplaetze.Core.Features.Reports;

/// <summary>Stand des Kommentars einer Meldung. Öffentlich nur <see cref="Approved"/>.</summary>
public enum ReportCommentStatus
{
    /// <summary>Kein Kommentar.</summary>
    None,

    /// <summary>Wartet auf die Moderation.</summary>
    Pending,

    /// <summary>Freigegeben, öffentlich.</summary>
    Approved,
}

/// <summary>Warum eine Meldung oder eine Aktion der Moderation abgelehnt ist.</summary>
public enum ReportError
{
    /// <summary>Konto fehlt, ist gesperrt oder nicht bestätigt.</summary>
    NotAllowed,

    /// <summary>Platz unbekannt oder nicht veröffentlicht.</summary>
    SiteNotFound,

    /// <summary>Datum in der Zukunft oder mehr als 7 Tage zurück.</summary>
    InvalidDate,

    /// <summary>Sicht ausserhalb 0.5–30 m oder nicht in Schritten von 0.5 m.</summary>
    InvalidVisibility,

    /// <summary>Temperatur ausserhalb −2 bis 30 °C.</summary>
    InvalidTemperature,

    /// <summary>Tiefe ausserhalb 0–100 m oder ohne Temperatur.</summary>
    InvalidDepth,

    /// <summary>Kommentar länger als 280 Zeichen.</summary>
    CommentTooLong,

    /// <summary>Mehr als 10 Meldungen an einem Tag.</summary>
    DailyLimit,

    /// <summary>Meldung unbekannt.</summary>
    NotFound,
}

/// <summary>Ergebnis eines Commands zu Meldungen.</summary>
public record ReportResult(IReadOnlyList<ReportError> Errors, Guid? ReportId = null)
{
    /// <summary>Ohne Fehler.</summary>
    public bool Succeeded => Errors.Count == 0;
}

/// <summary>Eine Meldung auf der Platzseite. <see cref="Comment"/> nur, wenn die Person ihn sehen darf; <see cref="AuthorName"/> nur mit Einwilligung.</summary>
public record SiteReport(
    Guid Id,
    DateOnly Date,
    decimal Visibility,
    decimal? Temperature,
    int? TemperatureDepth,
    string? Comment,
    ReportCommentStatus CommentStatus,
    string? AuthorName,
    bool IsOwn,
    decimal? ForecastIndex);

/// <summary>Eine Meldung in der Moderation.</summary>
public record QueuedReport(
    Guid Id,
    Guid DiveSiteId,
    string SiteName,
    string SiteSlug,
    DateOnly Date,
    decimal Visibility,
    decimal? Temperature,
    string? Comment,
    ReportCommentStatus CommentStatus,
    string? AuthorName,
    DateTimeOffset UpdatedAt,
    bool IsHidden);

/// <summary>Grenzen einer Meldung (`dive-site.md`, «Meldungen»).</summary>
public static class ReportRules
{
    /// <summary>Wie viele Tage zurück ein Tauchgang gemeldet werden kann.</summary>
    public const int MaxDaysBack = 7;

    /// <summary>Meldungen pro Konto und Kalendertag.</summary>
    public const int MaxPerDay = 10;

    /// <summary>Länge des Kommentars.</summary>
    public const int CommentMaxLength = 280;

    /// <summary>Wie viele Tage die Platzseite zurückschaut.</summary>
    public const int ShownDays = 14;

    /// <summary>Wie viele Meldungen die Platzseite höchstens zeigt.</summary>
    public const int ShownCount = 5;

    /// <summary>Kleinste Sicht in m.</summary>
    public const decimal MinVisibility = 0.5m;

    /// <summary>Grösste Sicht in m.</summary>
    public const decimal MaxVisibility = 30m;

    /// <summary>Tiefste Temperatur in °C.</summary>
    public const decimal MinTemperature = -2m;

    /// <summary>Höchste Temperatur in °C.</summary>
    public const decimal MaxTemperature = 30m;

    /// <summary>Grösste Tiefe der Temperatur in m.</summary>
    public const int MaxDepth = 100;
}
