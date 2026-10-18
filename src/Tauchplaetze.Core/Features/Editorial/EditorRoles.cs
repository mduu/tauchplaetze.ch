using Microsoft.AspNetCore.Identity;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Editorial;

/// <summary>Was ein Konto in der Redaktion darf. Der Core prüft das selbst und vertraut der Fassade nicht.</summary>
internal record EditorRights(bool Exists, bool IsBlocked, bool IsModerator);

internal class EditorRoles(UserManager<User> users)
{
    public async Task<EditorRights> Of(string userId)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null)
        {
            return new EditorRights(false, false, false);
        }

        var roles = await users.GetRolesAsync(user);
        return new EditorRights(true, user.BlockedAt is not null, roles.Contains(Roles.Moderator) || roles.Contains(Roles.Admin));
    }
}
