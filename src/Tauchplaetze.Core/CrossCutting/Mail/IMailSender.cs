namespace Tauchplaetze.Core.CrossCutting.Mail;

/// <summary>Eine Mail mit Text und, wenn vorhanden, HTML. <paramref name="ReplyTo"/>: Antworten gehen dorthin statt an den Absender.</summary>
internal record MailMessage(string To, string Subject, string Body, string? ReplyTo = null, string? Html = null);

/// <summary>Versand von Mails (lokal an Mailpit, `infrastructure.md`).</summary>
internal interface IMailSender
{
    /// <summary>Sendet die Mail. Ein Fehler wirft eine Exception: Der Aufrufer entscheidet, ob er es wieder versucht.</summary>
    Task Send(MailMessage message, CancellationToken ct);
}
