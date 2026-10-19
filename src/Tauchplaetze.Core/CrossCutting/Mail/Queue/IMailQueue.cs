namespace Tauchplaetze.Core.CrossCutting.Mail.Queue;

/// <summary>
/// Queue für Mails mit Dead-Letter-Queue (ADR-010). In Prod Azure Storage Queues, lokal Azurite.
/// Eine empfangene Nachricht ist bis zum Ablauf der Sichtbarkeit für andere unsichtbar. Gelöscht wird sie erst nach dem Senden.
/// </summary>
internal interface IMailQueue
{
    /// <summary>Legt beide Queues an, falls sie fehlen.</summary>
    Task EnsureCreated(CancellationToken ct);

    Task Enqueue(string body, CancellationToken ct);

    Task<IReadOnlyList<QueuedMessage>> Receive(int max, TimeSpan visibility, CancellationToken ct);

    Task Delete(QueuedMessage message, CancellationToken ct);

    /// <summary>Macht die Nachricht erst nach <paramref name="delay"/> wieder sichtbar (Backoff).</summary>
    Task Postpone(QueuedMessage message, TimeSpan delay, CancellationToken ct);

    /// <summary>Legt eine Nachricht in die Dead-Letter-Queue. Sie läuft nach der eingestellten Frist ab.</summary>
    Task DeadLetter(string body, CancellationToken ct);

    Task<IReadOnlyList<QueuedMessage>> ReceiveDeadLetters(int max, TimeSpan visibility, CancellationToken ct);

    Task DeleteDeadLetter(QueuedMessage message, CancellationToken ct);

    /// <summary>Gibt eine empfangene Dead-Letter-Nachricht sofort wieder frei.</summary>
    Task ReleaseDeadLetter(QueuedMessage message, CancellationToken ct);

    /// <summary>Liest höchstens 32 Dead-Letter-Nachrichten, ohne sie zu sperren (Grenze von Storage Queues).</summary>
    Task<IReadOnlyList<string>> PeekDeadLetters(CancellationToken ct);

    Task<MailQueueCounts> Counts(CancellationToken ct);
}

/// <summary>Ungefähre Zahl der Nachrichten (Storage Queues zählen nur ungefähr).</summary>
internal record MailQueueCounts(int Waiting, int DeadLetters);
