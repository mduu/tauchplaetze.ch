using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Options;

namespace Tauchplaetze.Core.CrossCutting.Mail.Queue;

/// <summary>
/// Mail-Queue auf Azure Storage Queues (ADR-010). Angelegt werden die Queues beim Start über <see cref="EnsureCreated"/>.
/// Lesende Aufrufe legen nichts an: Fehlt eine Queue, ist sie leer.
/// </summary>
internal class StorageMailQueue(QueueServiceClient service, IOptions<MailOptions> options) : IMailQueue
{
    public const string MainName = "mail-jobs";

    public const string DeadLetterName = "mail-jobs-deadletter";

    private QueueClient Main => service.GetQueueClient(MainName);

    private QueueClient Dead => service.GetQueueClient(DeadLetterName);

    public async Task EnsureCreated(CancellationToken ct)
    {
        await Main.CreateIfNotExistsAsync(cancellationToken: ct);
        await Dead.CreateIfNotExistsAsync(cancellationToken: ct);
    }

    public async Task Enqueue(string body, CancellationToken ct) => await Main.SendMessageAsync(body, cancellationToken: ct);

    public async Task<IReadOnlyList<QueuedMessage>> Receive(int max, TimeSpan visibility, CancellationToken ct) =>
        Map(await Missing(async () => (await Main.ReceiveMessagesAsync(max, visibility, ct)).Value, []));

    public async Task Delete(QueuedMessage message, CancellationToken ct) => await Main.DeleteMessageAsync(message.Id, message.PopReceipt, ct);

    public async Task Postpone(QueuedMessage message, TimeSpan delay, CancellationToken ct) =>
        await Main.UpdateMessageAsync(message.Id, message.PopReceipt, visibilityTimeout: delay, cancellationToken: ct);

    /// <summary>Eine Dead-Letter-Mail läuft nach <see cref="MailOptions.DeadLetterRetentionDays"/> ab (Datenschutz, Standard 30 Tage).</summary>
    public async Task DeadLetter(string body, CancellationToken ct) =>
        await Dead.SendMessageAsync(body, timeToLive: TimeSpan.FromDays(options.Value.DeadLetterRetentionDays), cancellationToken: ct);

    public async Task<IReadOnlyList<QueuedMessage>> ReceiveDeadLetters(int max, TimeSpan visibility, CancellationToken ct) =>
        Map(await Missing(async () => (await Dead.ReceiveMessagesAsync(max, visibility, ct)).Value, []));

    public async Task DeleteDeadLetter(QueuedMessage message, CancellationToken ct) => await Dead.DeleteMessageAsync(message.Id, message.PopReceipt, ct);

    public async Task ReleaseDeadLetter(QueuedMessage message, CancellationToken ct) =>
        await Dead.UpdateMessageAsync(message.Id, message.PopReceipt, visibilityTimeout: TimeSpan.Zero, cancellationToken: ct);

    public async Task<IReadOnlyList<string>> PeekDeadLetters(CancellationToken ct) =>
        await Missing<IReadOnlyList<string>>(async () => [.. (await Dead.PeekMessagesAsync(32, ct)).Value.Select(m => m.Body.ToString())], []);

    public async Task<MailQueueCounts> Counts(CancellationToken ct) => new(
        await Missing(async () => (await Main.GetPropertiesAsync(ct)).Value.ApproximateMessagesCount, 0),
        await Missing(async () => (await Dead.GetPropertiesAsync(ct)).Value.ApproximateMessagesCount, 0));

    private static async Task<T> Missing<T>(Func<Task<T>> read, T empty)
    {
        try
        {
            return await read();
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return empty;
        }
    }

    private static List<QueuedMessage> Map(QueueMessage[] messages) =>
        [.. messages.Select(m => new QueuedMessage(m.MessageId, m.PopReceipt, m.Body.ToString(), m.DequeueCount))];
}
