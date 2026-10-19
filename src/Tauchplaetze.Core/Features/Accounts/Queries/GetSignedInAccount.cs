using Microsoft.AspNetCore.Identity;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Accounts.Queries;

/// <summary>
/// Das angemeldete Konto frisch aus der Datenbank, mit denselben Regeln wie die Anmeldung.
/// null: Das Konto gibt es nicht mehr, oder es ist gesperrt oder unbestätigt. Die Web-App prüft damit ihr Cookie.
/// </summary>
public record GetSignedInAccountQuery(string UserId);

internal class GetSignedInAccountHandler(UserManager<User> users) : IQueryHandler<GetSignedInAccountQuery, SignedInAccount?>
{
    public async Task<SignedInAccount?> Handle(GetSignedInAccountQuery query, CancellationToken ct = default)
    {
        if (await users.FindByIdAsync(query.UserId) is not { BlockedAt: null, EmailConfirmed: true } user)
        {
            return null;
        }

        return new SignedInAccount(user.Id, user.DisplayName, user.PreferredCulture, [.. await users.GetRolesAsync(user)]);
    }
}
