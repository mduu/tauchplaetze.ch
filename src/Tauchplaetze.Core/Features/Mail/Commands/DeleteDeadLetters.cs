using Tauchplaetze.Core.CrossCutting.Mail.Queue;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Mail.Commands;

/// <summary>Löscht Dead-Letter-Mails endgültig: eine über <paramref name="MessageId"/> oder alle. Nur Admins.</summary>
public record DeleteDeadLettersCommand(string AdminId, Guid? MessageId = null);

internal class DeleteDeadLettersHandler(IMailQueue queue, AccountRoles roles) : ICommandHandler<DeleteDeadLettersCommand, DeadLetterActionResult>
{
    public async Task<DeadLetterActionResult> Handle(DeleteDeadLettersCommand command, CancellationToken ct = default)
    {
        if (!(await roles.Of(command.AdminId)).CanAdminister)
        {
            return new DeadLetterActionResult(0, NotAllowed: true);
        }

        var count = await DeadLetterScan.Apply(queue, command.MessageId, (message, _) => queue.DeleteDeadLetter(message, ct), ct);
        return new DeadLetterActionResult(count);
    }
}
