using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Editorial.Commands;

/// <summary>Gibt einen Vorschlag frei: Sein Inhalt geht online (`cms.md`, «Ablauf»). Nur Moderation und Admin.</summary>
public record ApproveRevisionCommand(string ReviewerId, Guid RevisionId);

internal class ApproveRevisionHandler(TauchplaetzeDbContext dbContext, RevisionWriter revisions, AccountRoles roles, ReviewMails mails, UserManager<User> users)
    : ICommandHandler<ApproveRevisionCommand, EditorialResult>
{
    public async Task<EditorialResult> Handle(ApproveRevisionCommand command, CancellationToken ct = default)
    {
        if (await roles.Of(command.ReviewerId) is not { IsModerator: true, IsBlocked: false })
        {
            return EditorialResult.Fail(EditorialError.NotAllowed);
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

        var site = await dbContext.DiveSites.Include(d => d.Translations).Include(d => d.EntryPoints).ThenInclude(e => e.Translations)
            .SingleAsync(d => d.Id == revision.DiveSiteId, ct);
        var content = await revisions.Load(revision, ct);
        if (content.Slug != site.Slug && await dbContext.DiveSites.AnyAsync(d => d.Slug == content.Slug && d.Id != site.Id, ct))
        {
            return EditorialResult.Fail(EditorialError.InvalidSlug);
        }

        await revisions.Approve(site, revision, content, command.ReviewerId, ct);
        await dbContext.SaveChangesAsync(ct);

        var author = revision.AuthorId is null ? null : await users.FindByIdAsync(revision.AuthorId);
        await mails.Send(revision, site.Name, "Approved", mails.SiteLink(author?.PreferredCulture ?? "de", site.Slug), ct);
        return new EditorialResult([], site.Id, revision.Number, revision.Status);
    }
}
