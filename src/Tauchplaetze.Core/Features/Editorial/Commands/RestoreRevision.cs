using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Editorial.Commands;

/// <summary>Stellt eine frühere Version wieder her: als neue, freigegebene Version. Nichts wird überschrieben. Nur Moderation und Admin.</summary>
public record RestoreRevisionCommand(string EditorId, Guid DiveSiteId, int Number);

internal class RestoreRevisionHandler(TauchplaetzeDbContext dbContext, RevisionWriter revisions, AccountRoles roles, EditorialNotifications notifications) : ICommandHandler<RestoreRevisionCommand, EditorialResult>
{
    public async Task<EditorialResult> Handle(RestoreRevisionCommand command, CancellationToken ct = default)
    {
        var rights = await roles.Of(command.EditorId);
        if (!rights.CanModerate)
        {
            return EditorialResult.Fail(rights.IsBlocked ? EditorialError.Blocked : EditorialError.NotAllowed);
        }

        var source = await dbContext.DiveSiteRevisions.SingleOrDefaultAsync(r => r.DiveSiteId == command.DiveSiteId && r.Number == command.Number, ct);
        var site = await dbContext.DiveSites.Include(d => d.Translations).Include(d => d.EntryPoints).ThenInclude(e => e.Translations).Include(d => d.Notices).ThenInclude(n => n.Translations)
            .SingleOrDefaultAsync(d => d.Id == command.DiveSiteId, ct);
        if (source is null || site is null)
        {
            return EditorialResult.Fail(EditorialError.NotFound);
        }

        if (source.RedactedAt is not null)
        {
            return EditorialResult.Fail(EditorialError.RedactedRevision);
        }

        var latestApproved = await dbContext.DiveSiteRevisions.Where(r => r.DiveSiteId == site.Id && r.Status == RevisionStatus.Approved).MaxAsync(r => (int?)r.Number, ct);
        var content = await revisions.Load(source, ct);
        var revision = await revisions.Append(site, content, latestApproved, command.EditorId, $"#{source.Number}", approve: true, ct);
        await dbContext.SaveChangesAsync(ct);
        await notifications.ModeratorChanged(revision, site.Id, content.Name, ct);
        return new EditorialResult([], site.Id, revision.Number, revision.Status);
    }
}
