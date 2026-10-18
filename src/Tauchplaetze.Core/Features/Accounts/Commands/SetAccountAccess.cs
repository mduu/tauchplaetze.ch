using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Editorial;

namespace Tauchplaetze.Core.Features.Accounts.Commands;

/// <summary>
/// Sperrt ein Konto oder hebt die Sperre auf (Moderation und Admin). Beim Sperren werden die offenen Vorschläge abgelehnt (`cms.md`, «Schutz gegen Missbrauch»).
/// </summary>
public record BlockAccountCommand(string ModeratorId, string UserId, bool Blocked);

internal class BlockAccountHandler(TauchplaetzeDbContext dbContext, UserManager<User> users, EditorRoles roles, TimeProvider time) : ICommandHandler<BlockAccountCommand, AccountResult>
{
    public async Task<AccountResult> Handle(BlockAccountCommand command, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(command.UserId);
        if (await roles.Of(command.ModeratorId) is not { IsModerator: true, IsBlocked: false } || user is null || user.Id == command.ModeratorId)
        {
            return AccountResult.Fail(AccountError.Other);
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
