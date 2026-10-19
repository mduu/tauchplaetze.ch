using System.Text.Json;
using Tauchplaetze.Core.CrossCutting.Mail.Queue;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Mail.Commands;

/// <summary>
/// Schickt Dead-Letter-Mails zurück in die Queue: eine über <paramref name="MessageId"/> oder alle. Sie beginnen wieder beim ersten Versuch.
/// Unlesbare Nachrichten bleiben liegen. Nur Admins. Konto-Mails tragen Links mit Ablauf: Sie können abgelaufen sein.
/// </summary>
public record ReplayDeadLettersCommand(string AdminId, Guid? MessageId = null);

internal class ReplayDeadLettersHandler(IMailQueue queue, AccountRoles roles) : ICommandHandler<ReplayDeadLettersCommand, DeadLetterActionResult>
{
    public async Task<DeadLetterActionResult> Handle(ReplayDeadLettersCommand command, CancellationToken ct = default)
    {
        if (!(await roles.Of(command.AdminId)).CanAdminister)
        {
            return new DeadLetterActionResult(0, NotAllowed: true);
        }

        var count = await DeadLetterScan.Apply(queue, command.MessageId, async (message, deadLetter) =>
        {
            if (deadLetter?.Envelope is not { } envelope)
            {
                return false;
            }

            await queue.Enqueue(JsonSerializer.Serialize(envelope, MailJson.Options), ct);
            await queue.DeleteDeadLetter(message, ct);
            return true;
        }, ct);
        return new DeadLetterActionResult(count);
    }
}
