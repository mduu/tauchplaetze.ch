using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Core.CrossCutting.Mail.Queue;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Mail.Queries;

/// <summary>Zähler der Mail-Queue und die Dead-Letter-Mails (`cms.md`, «Plattform»). Nur Admins, sonst <c>null</c>.</summary>
public record GetMailQueueStatusQuery(string AdminId);

internal class GetMailQueueStatusHandler(IMailQueue queue, MailDispatcher dispatcher, AccountRoles roles) : IQueryHandler<GetMailQueueStatusQuery, MailQueueStatus?>
{
    public async Task<MailQueueStatus?> Handle(GetMailQueueStatusQuery query, CancellationToken ct = default)
    {
        if (!(await roles.Of(query.AdminId)).CanAdminister)
        {
            return null;
        }

        var counts = await queue.Counts(ct);
        var items = (await queue.PeekDeadLetters(ct)).Select(body => Item(body, dispatcher)).ToList();
        return new MailQueueStatus(counts.Waiting, counts.DeadLetters, items);
    }

    private static DeadLetterItem Item(string body, MailDispatcher dispatcher)
    {
        if (DeadLetterScan.Read(body) is not { } deadLetter)
        {
            return new DeadLetterItem(null, null, null, null, 0, null, null, null, Unreadable: true);
        }

        var envelope = deadLetter.Envelope;
        return new DeadLetterItem(deadLetter.MessageId, envelope?.Template, envelope?.To, envelope is null ? null : Subject(envelope, dispatcher),
            deadLetter.Attempts, deadLetter.Reason, deadLetter.FailedAt, envelope?.CreatedAt);
    }

    private static string? Subject(MailEnvelope envelope, MailDispatcher dispatcher)
    {
        try
        {
            return dispatcher.Render(envelope).Subject;
        }
        catch (MailTemplateException)
        {
            return null;
        }
    }
}
