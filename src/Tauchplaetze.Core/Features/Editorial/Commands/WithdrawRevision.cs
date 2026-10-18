using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;

namespace Tauchplaetze.Core.Features.Editorial.Commands;

/// <summary>Zieht einen eigenen Vorschlag zurück, solange er auf Freigabe wartet.</summary>
public record WithdrawRevisionCommand(string EditorId, Guid RevisionId);

internal class WithdrawRevisionHandler(TauchplaetzeDbContext dbContext) : ICommandHandler<WithdrawRevisionCommand, EditorialResult>
{
    public async Task<EditorialResult> Handle(WithdrawRevisionCommand command, CancellationToken ct = default)
    {
        var revision = await dbContext.DiveSiteRevisions.SingleOrDefaultAsync(r => r.Id == command.RevisionId && r.AuthorId == command.EditorId, ct);
        if (revision is null)
        {
            return EditorialResult.Fail(EditorialError.NotFound);
        }

        if (revision.Status != RevisionStatus.Pending)
        {
            return EditorialResult.Fail(EditorialError.NotPending);
        }

        revision.Status = RevisionStatus.Withdrawn;
        await dbContext.SaveChangesAsync(ct);
        return new EditorialResult([], revision.DiveSiteId, revision.Number, revision.Status);
    }
}
