using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Editorial.Commands;

/// <summary>Zieht einen eigenen Vorschlag zurück, solange er auf Freigabe wartet. Nicht bei gesperrten Konten.</summary>
public record WithdrawRevisionCommand(string EditorId, Guid RevisionId);

internal class WithdrawRevisionHandler(TauchplaetzeDbContext dbContext, AccountRoles roles) : ICommandHandler<WithdrawRevisionCommand, EditorialResult>
{
    public async Task<EditorialResult> Handle(WithdrawRevisionCommand command, CancellationToken ct = default)
    {
        if (!(await roles.Of(command.EditorId)).CanWrite)
        {
            return EditorialResult.Fail(EditorialError.Blocked);
        }

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
