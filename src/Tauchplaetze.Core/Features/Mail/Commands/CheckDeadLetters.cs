using Microsoft.Extensions.Logging;
using Tauchplaetze.Core.CrossCutting.Mail.Queue;
using Tauchplaetze.Core.CrossCutting.UseCases;

namespace Tauchplaetze.Core.Features.Mail.Commands;

/// <summary>
/// Prüft die Dead-Letter-Queue. Liegt etwas darin, schreibt der Command einen Fehler ins Log. Darauf hört der Alert in Prod (`infrastructure.md`).
/// Liefert die Zahl der Dead-Letter-Mails.
/// </summary>
public record CheckDeadLettersCommand;

internal class CheckDeadLettersHandler(IMailQueue queue, ILogger<CheckDeadLettersHandler> logger) : ICommandHandler<CheckDeadLettersCommand, int>
{
    public async Task<int> Handle(CheckDeadLettersCommand command, CancellationToken ct = default)
    {
        var counts = await queue.Counts(ct);
        if (counts.DeadLetters > 0)
        {
            logger.LogError("Deadletter queue {Queue} has {Count} mails awaiting investigation", StorageMailQueue.DeadLetterName, counts.DeadLetters);
        }

        return counts.DeadLetters;
    }
}
