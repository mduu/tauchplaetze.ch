using Tauchplaetze.Core.Features.Editorial;

namespace Tauchplaetze.Core.Features.Translations;

public enum TranslationError
{
    /// <summary>Konto fehlt oder ist gesperrt.</summary>
    NotAllowed,

    /// <summary>Kein Schlüssel konfiguriert.</summary>
    Unavailable,

    /// <summary>Unbekannte Sprache, gleiche Sprache, kein Text oder zu lang.</summary>
    InvalidInput,

    DailyLimit,

    BudgetExhausted,

    /// <summary>Die Claude API antwortet nicht oder die Übersetzung verändert das Markdown.</summary>
    Failed,
}

/// <summary>Die übersetzten Texte mit <c>IsMachineTranslated = true</c> oder ein Fehler.</summary>
public record TranslationResult(DiveSiteContentTranslation? Text, TranslationError? Error)
{
    public static TranslationResult Fail(TranslationError error) => new(null, error);
}

/// <summary>Was das Konto jetzt am Übersetzen hindert (<c>null</c>: nichts), und wie viele Übersetzungen heute noch gehen (<c>null</c>: kein Tageslimit).</summary>
public record TranslationAllowance(TranslationError? Blocker, int? RemainingToday)
{
    public bool Available => Blocker is null;
}
