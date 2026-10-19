using System.Text.Json;
using Tauchplaetze.Core.CrossCutting.Mail.Queue;

namespace Tauchplaetze.Core.Features.Mail;

/// <summary>
/// Geht die Dead-Letter-Queue durch. Storage Queues finden keine Nachricht über ihre Id, darum empfängt der Scan
/// in Paketen, wendet die Aktion auf die passenden an und gibt die übrigen sofort wieder frei, auch nach einem Fehler (ADR-010).
/// «Alle» heisst: höchstens so viele, wie beim Start der Aktion da waren. Was währenddessen neu scheitert, bleibt meist liegen.
/// </summary>
internal static class DeadLetterScan
{
    private const int Batch = 32;

    private const int Limit = 1000;

    /// <summary>
    /// Wendet <paramref name="action"/> auf die Nachricht mit <paramref name="messageId"/> an, ohne Id auf alle.
    /// Gibt die Aktion <c>false</c> zurück, bleibt die Nachricht liegen und zählt nicht.
    /// </summary>
    public static async Task<int> Apply(IMailQueue queue, Guid? messageId, Func<QueuedMessage, DeadLetter?, Task<bool>> action, CancellationToken ct)
    {
        var max = messageId is null ? Math.Min((await queue.Counts(ct)).DeadLetters, Limit) : 1;
        var seen = new HashSet<string>();
        var count = 0;
        while (count < max && seen.Count < Limit)
        {
            var batch = await queue.ReceiveDeadLetters(Batch, TimeSpan.FromMinutes(2), ct);
            if (batch.Count == 0)
            {
                return count;
            }

            var handled = new HashSet<string>();
            var done = false;
            try
            {
                foreach (var message in batch)
                {
                    done |= !seen.Add(message.Id);
                    var deadLetter = Read(message.Body);
                    if (done || count >= max || (messageId is { } id && deadLetter?.MessageId != id))
                    {
                        continue;
                    }

                    if (!await action(message, deadLetter))
                    {
                        continue;
                    }

                    handled.Add(message.Id);
                    count++;
                    if (messageId is not null)
                    {
                        done = true;
                        break;
                    }
                }
            }
            finally
            {
                foreach (var message in batch.Where(m => !handled.Contains(m.Id)))
                {
                    await queue.ReleaseDeadLetter(message, CancellationToken.None);
                }
            }

            if (done)
            {
                return count;
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
