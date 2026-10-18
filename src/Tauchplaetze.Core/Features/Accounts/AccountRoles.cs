using Microsoft.AspNetCore.Identity;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Accounts;

/// <summary>
/// Was ein Konto darf, frisch aus der Datenbank. Der Core prüft das selbst und vertraut der Fassade nicht:
/// Rollen im Cookie gelten bis zur nächsten Anmeldung.
/// </summary>
internal record AccountRights(bool Exists, bool IsBlocked, bool IsModerator, bool IsAdmin)
{
    /// <summary>Darf schreiben: Konto vorhanden und nicht gesperrt.</summary>
    public bool CanWrite => Exists && !IsBlocked;
}

internal class AccountRoles(UserManager<User> users)
{
    public async Task<AccountRights> Of(string userId)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null)
        {
            return new AccountRights(false, false, false, false);
        }

        var roles = await users.GetRolesAsync(user);
        var isAdmin = roles.Contains(Roles.Admin);
        return new AccountRights(true, user.BlockedAt is not null, isAdmin || roles.Contains(Roles.Moderator), isAdmin);
    }
}
