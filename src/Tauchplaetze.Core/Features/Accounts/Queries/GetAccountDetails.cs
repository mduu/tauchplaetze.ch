using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Editorial;

namespace Tauchplaetze.Core.Features.Accounts.Queries;

/// <summary>Ein Konto im Detail für die Plattform (`cms.md`, «Konten»). Nur für Admins; sonst und für ein unbekanntes Konto null.</summary>
public record GetAccountDetailsQuery(string AdminId, string AccountId);

/// <summary>
/// Angaben, Anmeldemethoden und Beiträge eines Kontos. <see cref="CreatedAt"/> und <see cref="LastSignInAt"/> sind bei alten Konten null.
/// <see cref="LockoutEnd"/>: gesperrt nach zu vielen Fehlversuchen bis dann (Identity), nicht die Sperre durch die Moderation (<see cref="BlockedAt"/>).
/// </summary>
public record AccountDetails(
    string UserId,
    string DisplayName,
    string Email,
    bool EmailConfirmed,
    string PreferredCulture,
    bool ShowNamePublicly,
    IReadOnlyList<string> Roles,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? LastSignInAt,
    int AccessFailedCount,
    DateTimeOffset? LockoutEnd,
    bool HasPassword,
    IReadOnlyList<string> ExternalLogins,
    DateTimeOffset? BlockedAt,
    string? BlockedByName,
    int Revisions,
    int PendingProposals,
    int Photos);

internal class GetAccountDetailsHandler(TauchplaetzeDbContext dbContext, UserManager<User> users, AccountRoles roles) : IQueryHandler<GetAccountDetailsQuery, AccountDetails?>
{
    public async Task<AccountDetails?> Handle(GetAccountDetailsQuery query, CancellationToken ct = default)
    {
        if (!(await roles.Of(query.AdminId)).CanAdminister || await users.FindByIdAsync(query.AccountId) is not { } user)
        {
            return null;
        }

        var blockedBy = user.BlockedBy is null ? null : await dbContext.Users.Where(u => u.Id == user.BlockedBy).Select(u => u.DisplayName).FirstOrDefaultAsync(ct);
        var revisions = dbContext.DiveSiteRevisions.Where(r => r.AuthorId == user.Id);

        return new AccountDetails(
            user.Id,
            user.DisplayName,
            user.Email ?? "",
            user.EmailConfirmed,
            user.PreferredCulture,
            user.ShowNamePublicly,
            [.. (await users.GetRolesAsync(user)).Order(StringComparer.Ordinal)],
            user.CreatedAt,
            user.LastSignInAt,
            user.AccessFailedCount,
            user.LockoutEnd,
            await users.HasPasswordAsync(user),
            [.. (await users.GetLoginsAsync(user)).Select(l => l.ProviderDisplayName ?? l.LoginProvider)],
            user.BlockedAt,
            blockedBy,
            await revisions.CountAsync(ct),
            await revisions.CountAsync(r => r.Status == RevisionStatus.Pending, ct),
            await dbContext.MediaItems.CountAsync(m => m.UploadedBy == user.Id, ct));
    }
}
