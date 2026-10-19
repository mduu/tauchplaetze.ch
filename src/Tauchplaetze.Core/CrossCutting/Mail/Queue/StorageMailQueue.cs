using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

namespace Tauchplaetze.Core.CrossCutting.Mail.Queue;

/// <summary>Mail-Queue auf Azure Storage Queues. Die Queues entstehen beim ersten Zugriff des Prozesses.</summary>
internal class StorageMailQueue(QueueServiceClient service) : IMailQueue
{
    public const string MainName = "mail-jobs";

    public const string DeadLetterName = "mail-jobs-deadletter";

    /// <summary>Ohne Ablauf: Der Standard von Storage Queues löscht nach 7 Tagen still.</summary>
    private static readonly TimeSpan NeverExpires = TimeSpan.FromSeconds(-1);

    private readonly SemaphoreSlim creating = new(1, 1);

    private volatile bool created;

    public async Task Enqueue(string body, CancellationToken ct) => await (await Main()).SendMessageAsync(body, cancellationToken: ct);

    public async Task<IReadOnlyList<QueuedMessage>> Receive(int max, TimeSpan visibility, CancellationToken ct) =>
        Map((await (await Main()).ReceiveMessagesAsync(max, visibility, ct)).Value);

    public async Task Delete(QueuedMessage message, CancellationToken ct) =>
        await (await Main()).DeleteMessageAsync(message.Id, message.PopReceipt, ct);

    public async Task Postpone(QueuedMessage message, TimeSpan delay, CancellationToken ct) =>
        await (await Main()).UpdateMessageAsync(message.Id, message.PopReceipt, visibilityTimeout: delay, cancellationToken: ct);

    public async Task DeadLetter(string body, CancellationToken ct) =>
        await (await Dead()).SendMessageAsync(body, timeToLive: NeverExpires, cancellationToken: ct);

    public async Task<IReadOnlyList<QueuedMessage>> ReceiveDeadLetters(int max, TimeSpan visibility, CancellationToken ct) =>
        Map((await (await Dead()).ReceiveMessagesAsync(max, visibility, ct)).Value);

    public async Task DeleteDeadLetter(QueuedMessage message, CancellationToken ct) =>
        await (await Dead()).DeleteMessageAsync(message.Id, message.PopReceipt, ct);

    public async Task ReleaseDeadLetter(QueuedMessage message, CancellationToken ct) =>
        await (await Dead()).UpdateMessageAsync(message.Id, message.PopReceipt, visibilityTimeout: TimeSpan.Zero, cancellationToken: ct);

    public async Task<IReadOnlyList<string>> PeekDeadLetters(CancellationToken ct) =>
        [.. (await (await Dead()).PeekMessagesAsync(32, ct)).Value.Select(m => m.Body.ToString())];

    public async Task<MailQueueCounts> Counts(CancellationToken ct)
    {
        var main = await Main();
        var dead = await Dead();
        var waiting = await main.GetPropertiesAsync(ct);
        var deadLetters = await dead.GetPropertiesAsync(ct);
        return new MailQueueCounts(waiting.Value.ApproximateMessagesCount, deadLetters.Value.ApproximateMessagesCount);
    }

    private async Task<QueueClient> Main() => await Created(service.GetQueueClient(MainName));

    private async Task<QueueClient> Dead() => await Created(service.GetQueueClient(DeadLetterName));

    /// <remarks>Scheitert das Anlegen (z.B. Azurite noch nicht bereit), versucht es der nächste Aufruf wieder.</remarks>
    private async Task<QueueClient> Created(QueueClient client)
    {
        if (created)
        {
            return client;
        }

        await creating.WaitAsync();
        try
        {
            if (!created)
            {
                await service.GetQueueClient(MainName).CreateIfNotExistsAsync();
                await service.GetQueueClient(DeadLetterName).CreateIfNotExistsAsync();
                created = true;
            }
        }
        finally
        {
            creating.Release();
        }

        return client;
    }

    private static List<QueuedMessage> Map(QueueMessage[] messages) =>
        [.. messages.Select(m => new QueuedMessage(m.MessageId, m.PopReceipt, m.Body.ToString(), m.DequeueCount))];
}
