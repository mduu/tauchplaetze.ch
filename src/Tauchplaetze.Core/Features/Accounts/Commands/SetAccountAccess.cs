using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Editorial;

namespace Tauchplaetze.Core.Features.Accounts.Commands;

/// <summary>
/// Sperrt ein Konto oder hebt die Sperre auf (Moderation und Admin). Ein Admin-Konto sperren nur Admins.
/// Beim Sperren werden die offenen Vorschläge abgelehnt (`cms.md`, «Schutz gegen Missbrauch»).
/// </summary>
public record BlockAccountCommand(string ModeratorId, string UserId, bool Blocked);

internal class BlockAccountHandler(TauchplaetzeDbContext dbContext, UserManager<User> users, AccountRoles roles, TimeProvider time) : ICommandHandler<BlockAccountCommand, AccountResult>
{
    public async Task<AccountResult> Handle(BlockAccountCommand command, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(command.UserId);
        if (user is null)
        {
            return AccountResult.Fail(AccountError.Other);
        }

        var moderator = await roles.Of(command.ModeratorId);
        var target = await roles.Of(user.Id);
        if (moderator is not { IsModerator: true, IsBlocked: false } || (target.IsAdmin && !moderator.IsAdmin) || user.Id == command.ModeratorId)
        {
            return AccountResult.Fail(AccountError.NotAllowed);
        }

        var now = time.GetUtcNow();
        user.BlockedAt = command.Blocked ? now : null;
        user.BlockedBy = command.Blocked ? command.ModeratorId : null;

        if (command.Blocked)
        {
            await users.UpdateSecurityStampAsync(user);
            await dbContext.DiveSiteRevisions
                .Where(r => r.AuthorId == user.Id && r.Status == RevisionStatus.Pending)
                .ExecuteUpdateAsync(r => r
                    .SetProperty(x => x.Status, RevisionStatus.Rejected)
                    .SetProperty(x => x.ReviewerId, command.ModeratorId)
                    .SetProperty(x => x.ReviewedAt, now), ct);
        }

        return AccountErrors.From(await users.UpdateAsync(user));
    }
}
