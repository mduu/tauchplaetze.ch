namespace Tauchplaetze.Web.Features.About;

/// <summary>Zustand des Kontaktformulars nach einem Redirect (`?kontakt=gesendet` bzw. `?kontakt=limit`).</summary>
public enum ContactStatus
{
    /// <summary>Formular leer anzeigen.</summary>
    Open,

    /// <summary>Die Nachricht ist gesendet.</summary>
    Sent,

    /// <summary>Das Rate Limit hat die Nachricht abgewiesen.</summary>
    Limited,
}
