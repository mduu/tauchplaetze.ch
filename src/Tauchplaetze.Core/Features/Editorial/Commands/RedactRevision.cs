using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Editorial.Commands;

/// <summary>
/// Schwärzt eine Version (`cms.md`, «Datenschutz in der History»): Sie verschwindet aus der öffentlichen History und dem Vergleich.
/// Im CMS bleibt sie mit Hinweis sichtbar. Nur Moderation und Admin. Die veröffentlichte Version lässt sich nicht schwärzen.
/// <see cref="Redact"/> <c>false</c> hebt die Schwärzung auf, nur Admin.
/// </summary>
public record RedactRevisionCommand(string ModeratorId, Guid DiveSiteId, int Number, bool Redact = true);

internal class RedactRevisionHandler(TauchplaetzeDbContext dbContext, AccountRoles roles, TimeProvider time) : ICommandHandler<RedactRevisionCommand, EditorialResult>
{
    public async Task<EditorialResult> Handle(RedactRevisionCommand command, CancellationToken ct = default)
    {
        var rights = await roles.Of(command.ModeratorId);
        if (command.Redact ? !rights.CanModerate : !rights.CanAdminister)
        {
            return EditorialResult.Fail(rights.IsBlocked ? EditorialError.Blocked : EditorialError.NotAllowed);
        }

        var revision = await dbContext.DiveSiteRevisions.SingleOrDefaultAsync(r => r.DiveSiteId == command.DiveSiteId && r.Number == command.Number, ct);
        if (revision is null)
        {
            return EditorialResult.Fail(EditorialError.NotFound);
        }

        if (command.Redact && revision.RedactedAt is null)
        {
            var published = await dbContext.DiveSiteRevisions
                .Where(r => r.DiveSiteId == command.DiveSiteId && r.Status == RevisionStatus.Approved)
                .MaxAsync(r => (int?)r.Number, ct);
            if (published == revision.Number)
            {
                return EditorialResult.Fail(EditorialError.PublishedRevision);
            }

            revision.RedactedAt = time.GetUtcNow();
            revision.RedactedBy = command.ModeratorId;
        }
        else if (!command.Redact && revision.RedactedAt is not null)
        {
            revision.RedactedAt = null;
            revision.RedactedBy = null;
        }

        await dbContext.SaveChangesAsync(ct);
        return new EditorialResult([], revision.DiveSiteId, revision.Number, revision.Status);
    }
}
