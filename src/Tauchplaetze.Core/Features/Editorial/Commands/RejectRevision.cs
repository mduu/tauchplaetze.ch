using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Editorial.Commands;

/// <summary>Lehnt einen Vorschlag mit Begründung ab. Die Autorin oder der Autor bekommt eine Mail. Nur Moderation und Admin.</summary>
public record RejectRevisionCommand(string ReviewerId, Guid RevisionId, string Reason);

internal class RejectRevisionHandler(TauchplaetzeDbContext dbContext, AccountRoles roles, EditorialNotifications notifications, TimeProvider time)
    : ICommandHandler<RejectRevisionCommand, EditorialResult>
{
    public async Task<EditorialResult> Handle(RejectRevisionCommand command, CancellationToken ct = default)
    {
        var rights = await roles.Of(command.ReviewerId);
        if (!rights.CanModerate)
        {
            return EditorialResult.Fail(rights.IsBlocked ? EditorialError.Blocked : EditorialError.NotAllowed);
        }

        var revision = await dbContext.DiveSiteRevisions.SingleOrDefaultAsync(r => r.Id == command.RevisionId, ct);
        if (revision is null)
        {
            return EditorialResult.Fail(EditorialError.NotFound);
        }

        if (revision.Status != RevisionStatus.Pending)
        {
            return EditorialResult.Fail(EditorialError.NotPending);
        }

        revision.Status = RevisionStatus.Rejected;
        revision.ReviewerId = command.ReviewerId;
        revision.ReviewedAt = time.GetUtcNow();
        revision.ReviewComment = string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason.Trim();
        await dbContext.SaveChangesAsync(ct);

        var siteName = await dbContext.DiveSites.Where(d => d.Id == revision.DiveSiteId).Select(d => d.Name).SingleAsync(ct);
        await notifications.Rejected(revision, siteName, ct);
        return new EditorialResult([], revision.DiveSiteId, revision.Number, revision.Status);
    }
}
