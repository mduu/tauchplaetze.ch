using System.Text.Json;
using Tauchplaetze.Core.CrossCutting.Mail.Queue;

namespace Tauchplaetze.Core.Features.Mail;

/// <summary>
/// Geht die Dead-Letter-Queue durch. Storage Queues finden keine Nachricht über ihre Id, darum empfängt der Scan
/// in Paketen, wendet die Aktion auf die passenden an und gibt die übrigen sofort wieder frei (ADR-010).
/// </summary>
internal static class DeadLetterScan
{
    private const int Limit = 1000;

    /// <summary>Wendet <paramref name="action"/> auf jede passende Nachricht an. Ohne <paramref name="messageId"/> passen alle.</summary>
    public static async Task<int> Apply(IMailQueue queue, Guid? messageId, Func<QueuedMessage, DeadLetter?, Task> action, CancellationToken ct)
    {
        var seen = new HashSet<string>();
        var count = 0;
        while (seen.Count < Limit)
        {
            var batch = await queue.ReceiveDeadLetters(32, TimeSpan.FromSeconds(30), ct);
            if (batch.Count == 0)
            {
                break;
            }

            var repeated = false;
            var found = false;
            foreach (var message in batch)
            {
                repeated |= !seen.Add(message.Id);
                var deadLetter = Read(message.Body);
                if (!found && (messageId is null || deadLetter?.MessageId == messageId))
                {
                    await action(message, deadLetter);
                    count++;
                    found = messageId is not null;
                }
                else
                {
                    await queue.ReleaseDeadLetter(message, ct);
                }
            }

            if (repeated || found)
            {
                break;
            }
        }

        return count;
    }

    public static DeadLetter? Read(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<DeadLetter>(body, MailJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
