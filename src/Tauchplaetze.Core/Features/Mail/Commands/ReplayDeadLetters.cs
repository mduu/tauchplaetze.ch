using System.Text.Json;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.Mail.Queue;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Contact;

namespace Tauchplaetze.Core.Features.Mail.Commands;

/// <summary>
/// Schickt Dead-Letter-Mails zurück in die Queue: eine über <paramref name="MessageId"/> oder alle. Sie beginnen wieder beim ersten Versuch.
/// Unlesbare Nachrichten bleiben liegen. Nur Admins. Konto-Mails tragen Links mit Ablauf: Sie können abgelaufen sein.
/// Eine Kontaktnachricht geht an die heutige Adresse des Betreibers (`Operator:Email`), nicht an die im Umschlag.
/// </summary>
public record ReplayDeadLettersCommand(string AdminId, Guid? MessageId = null);

internal class ReplayDeadLettersHandler(IMailQueue queue, AccountRoles roles, IOptions<ContactOptions> contact) : ICommandHandler<ReplayDeadLettersCommand, DeadLetterActionResult>
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

            await queue.Enqueue(JsonSerializer.Serialize(ToCurrentRecipient(envelope), MailJson.Options), ct);
            await queue.DeleteDeadLetter(message, ct);
            return true;
        }, ct);
        return new DeadLetterActionResult(count);
    }

    private MailEnvelope ToCurrentRecipient(MailEnvelope envelope) =>
        envelope.Template == ContactMailTemplate.TemplateName && !string.IsNullOrWhiteSpace(contact.Value.Email)
            ? envelope with { To = contact.Value.Email }
            : envelope;
}
