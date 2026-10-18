namespace Tauchplaetze.Core.CrossCutting.Mail;

/// <summary>Eine Mail als reiner Text. <paramref name="ReplyTo"/>: Antworten gehen dorthin statt an den Absender.</summary>
internal record MailMessage(string To, string Subject, string Body, string? ReplyTo = null);

/// <summary>Versand von Mails (lokal an Mailpit, `infrastructure.md`).</summary>
internal interface IMailSender
{
    /// <summary>Sendet die Mail. Ein Fehler wirft eine Exception: Der Command entscheidet, ob er weiterläuft.</summary>
    Task Send(MailMessage message, CancellationToken ct);
}
