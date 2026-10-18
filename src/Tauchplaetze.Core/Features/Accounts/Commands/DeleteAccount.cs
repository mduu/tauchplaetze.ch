using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Accounts.Commands;

/// <summary>
/// Löscht ein Konto. Die Versionen bleiben, Autor und Approver werden «gelöschtes Konto» (`cms.md`, «Datenschutz in der History»).
/// Der letzte aktive Admin löscht sich nicht.
/// </summary>
public record DeleteAccountCommand(string UserId);

internal class DeleteAccountHandler(TauchplaetzeDbContext dbContext, UserManager<User> users, AccountRoles roles) : ICommandHandler<DeleteAccountCommand, AccountResult>
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

        return AccountErrors.From(await users.DeleteAsync(user));
    }
}
