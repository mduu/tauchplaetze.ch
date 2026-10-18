using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.DiveSites;

namespace Tauchplaetze.Core.Features.Editorial.Commands;

/// <summary>Nimmt einen Platz offline (410) oder wieder online. Nur Moderation und Admin.</summary>
public record ArchiveDiveSiteCommand(string EditorId, Guid DiveSiteId, bool Archived);

internal class ArchiveDiveSiteHandler(TauchplaetzeDbContext dbContext, AccountRoles roles, TimeProvider time) : ICommandHandler<ArchiveDiveSiteCommand, EditorialResult>
{
    public async Task<EditorialResult> Handle(ArchiveDiveSiteCommand command, CancellationToken ct = default)
    {
        var rights = await roles.Of(command.EditorId);
        if (!rights.CanModerate)
        {
            return EditorialResult.Fail(rights.IsBlocked ? EditorialError.Blocked : EditorialError.NotAllowed);
        }

        var site = await dbContext.DiveSites.SingleOrDefaultAsync(d => d.Id == command.DiveSiteId && d.Status != DiveSiteStatus.Draft, ct);
        if (site is null)
        {
            return EditorialResult.Fail(EditorialError.NotFound);
        }

        site.Status = command.Archived ? DiveSiteStatus.Archived : DiveSiteStatus.Published;
        site.UpdatedAt = time.GetUtcNow();
        site.UpdatedBy = command.EditorId;
        await dbContext.SaveChangesAsync(ct);
        return new EditorialResult([], site.Id);
    }
}
