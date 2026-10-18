using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;

namespace Tauchplaetze.Core.Features.Accounts.Queries;

/// <summary>
/// Alle Konten für die Verwaltung im CMS, nach Anzeigename. Nur für Moderation und Admin, sonst leer.
/// Die E-Mail-Adresse sehen nur Admins.
/// </summary>
public record GetAccountsQuery(string RequesterId);

/// <summary>Ein Konto in der Verwaltung.</summary>
public record AccountSummary(string UserId, string DisplayName, string Email, bool EmailConfirmed, IReadOnlyList<string> Roles, DateTimeOffset? BlockedAt);

internal class GetAccountsHandler(TauchplaetzeDbContext dbContext, AccountRoles accountRoles) : IQueryHandler<GetAccountsQuery, IReadOnlyList<AccountSummary>>
{
    public async Task<IReadOnlyList<AccountSummary>> Handle(GetAccountsQuery query, CancellationToken ct = default)
    {
        var requester = await accountRoles.Of(query.RequesterId);
        if (!requester.IsModerator)
        {
            return [];
        }

        var roles = await dbContext.UserRoles
            .Join(dbContext.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync(ct);
        var users = await dbContext.Users.AsNoTracking().OrderBy(u => u.DisplayName).ToListAsync(ct);

        return users.Select(u => new AccountSummary(
                u.Id, u.DisplayName, requester.IsAdmin ? u.Email ?? "" : "", u.EmailConfirmed,
                roles.Where(r => r.UserId == u.Id).Select(r => r.Name!).Order().ToList(),
                u.BlockedAt))
            .ToList();
    }
}
