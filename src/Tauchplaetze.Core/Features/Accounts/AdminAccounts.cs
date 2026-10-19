using Microsoft.AspNetCore.Identity;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Accounts;

/// <summary>Gemeinsame Prüfung der Admin-Commands auf ein fremdes Konto: der Admin darf verwalten, das Konto gibt es.</summary>
internal static class AdminAccounts
{
    public static async Task<(User? User, AccountError? Error)> Target(UserManager<User> users, AccountRoles roles, string adminId, string accountId)
    {
        var admin = await roles.Of(adminId);
        if (admin.IsBlocked)
        {
            return (null, AccountError.Blocked);
        }

        if (!admin.CanAdminister)
        {
            return (null, AccountError.NotAllowed);
        }

        return await users.FindByIdAsync(accountId) is { } user ? (user, null) : (null, AccountError.Other);
    }
}
