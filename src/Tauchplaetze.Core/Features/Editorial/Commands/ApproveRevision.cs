using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Editorial.Commands;

/// <summary>Gibt einen Vorschlag frei: Sein Inhalt geht online (`cms.md`, «Ablauf»). Nur Moderation und Admin.</summary>
public record ApproveRevisionCommand(string ReviewerId, Guid RevisionId);

internal class ApproveRevisionHandler(TauchplaetzeDbContext dbContext, RevisionWriter revisions, AccountRoles roles, EditorialNotifications notifications)
    : ICommandHandler<ApproveRevisionCommand, EditorialResult>
{
    public async Task<EditorialResult> Handle(ApproveRevisionCommand command, CancellationToken ct = default)
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

        var site = await dbContext.DiveSites.Include(d => d.Translations).Include(d => d.EntryPoints).ThenInclude(e => e.Translations).Include(d => d.Notices).ThenInclude(n => n.Translations)
            .SingleAsync(d => d.Id == revision.DiveSiteId, ct);
        var content = await revisions.Load(revision, ct);
        if (content.Slug != site.Slug && await dbContext.DiveSites.AnyAsync(d => d.Slug == content.Slug && d.Id != site.Id, ct))
        {
            return EditorialResult.Fail(EditorialError.InvalidSlug);
        }

        await revisions.Approve(site, revision, content, command.ReviewerId, ct);
        await dbContext.SaveChangesAsync(ct);

        await notifications.Approved(revision, site.Name, site.Slug, ct);
        return new EditorialResult([], site.Id, revision.Number, revision.Status);
    }
}
