using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Media;

namespace Tauchplaetze.Core.Features.Accounts.Commands;

/// <summary>
/// Löscht ein Konto. Die Versionen bleiben, Autor und Approver werden «gelöschtes Konto» (`cms.md`, «Datenschutz in der History»).
/// Private Fotos werden gelöscht. Öffentliche bleiben mit dem Namen des Fotografen, ohne Bezug zum Konto. Der letzte aktive Admin löscht sich nicht.
/// </summary>
public record DeleteAccountCommand(string UserId);

internal class DeleteAccountHandler(TauchplaetzeDbContext dbContext, UserManager<User> users, AccountRoles roles, IMediaStore media) : ICommandHandler<DeleteAccountCommand, AccountResult>
{
    public async Task<AccountResult> Handle(DeleteAccountCommand command, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(command.UserId);
        if (user is null)
        {
            return AccountResult.Fail(AccountError.Other);
        }

        if (await roles.IsLastActiveAdmin(user.Id))
        {
            return AccountResult.Fail(AccountError.LastAdmin);
        }

        await dbContext.DiveSiteRevisions.Where(r => r.AuthorId == user.Id).ExecuteUpdateAsync(r => r.SetProperty(x => x.AuthorId, (string?)null), ct);
        await dbContext.DiveSiteRevisions.Where(r => r.ReviewerId == user.Id).ExecuteUpdateAsync(r => r.SetProperty(x => x.ReviewerId, (string?)null), ct);
        await dbContext.DiveSiteRevisions.Where(r => r.RedactedBy == user.Id).ExecuteUpdateAsync(r => r.SetProperty(x => x.RedactedBy, (string?)null), ct);
        await dbContext.DiveSites.Where(d => d.CreatedBy == user.Id).ExecuteUpdateAsync(d => d.SetProperty(x => x.CreatedBy, (string?)null), ct);
        await dbContext.DiveSites.Where(d => d.UpdatedBy == user.Id).ExecuteUpdateAsync(d => d.SetProperty(x => x.UpdatedBy, (string?)null), ct);
        await DeletePrivatePhotos(user.Id, ct);
        await dbContext.MediaItems.Where(m => m.UploadedBy == user.Id).ExecuteUpdateAsync(m => m.SetProperty(x => x.UploadedBy, (string?)null), ct);

        return AccountErrors.From(await users.DeleteAsync(user));
    }

    private async Task DeletePrivatePhotos(string userId, CancellationToken ct)
    {
        var ids = await dbContext.MediaItems.Where(m => m.UploadedBy == userId && !m.IsPublic).Select(m => m.Id).ToListAsync(ct);
        foreach (var id in ids)
        {
            await media.DeletePending(id, ct);
        }

        await dbContext.MediaItems.Where(m => ids.Contains(m.Id)).ExecuteDeleteAsync(ct);
    }
}
