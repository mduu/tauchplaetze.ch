namespace Tauchplaetze.Core.Features.Mail;

/// <summary>Stand der Mail-Queue für `/platform`. <see cref="Items"/>: höchstens 32 Dead-Letter-Mails (Grenze von Storage Queues).</summary>
public record MailQueueStatus(int Waiting, int DeadLetters, IReadOnlyList<DeadLetterItem> Items);

/// <summary>Eine Mail in der Dead-Letter-Queue. <see cref="Unreadable"/>: Die Nachricht liess sich nicht lesen, es gibt nur die Rohdaten.</summary>
public record DeadLetterItem(Guid? MessageId, string? Template, string? To, string? Subject, int Attempts, string? Reason, DateTimeOffset? FailedAt, DateTimeOffset? CreatedAt, bool Unreadable = false);

/// <summary>Ergebnis einer Aktion auf der Dead-Letter-Queue. <see cref="NotAllowed"/>: nur für Admins.</summary>
public record DeadLetterActionResult(int Count, bool NotAllowed = false);
