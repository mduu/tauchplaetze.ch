namespace Tauchplaetze.Core.CrossCutting.Mail;

/// <summary>SMTP-Verbindung (`Mail`). Lokal setzt Aspire Host und Port von Mailpit.</summary>
public class MailOptions
{
    public const string Section = "Mail";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1025;

    public string From { get; set; } = "tauchplaetze.ch <noreply@tauchplaetze.ch>";

    public string? UserName { get; set; }

    public string? Password { get; set; }

    /// <summary>TLS beim Verbinden. Lokal mit Mailpit aus.</summary>
    public bool UseTls { get; set; }

    /// <summary>Höchste Dauer eines SMTP-Befehls. Kurz, damit eine empfangene Mail nicht wieder sichtbar wird, bevor sie gesendet ist.</summary>
    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>So lange bleibt eine Mail in der Dead-Letter-Queue, dann löscht Azure sie (Datenschutz, ADR-010).</summary>
    public int DeadLetterRetentionDays { get; set; } = 30;
}
