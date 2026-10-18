using Microsoft.AspNetCore.Identity;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Accounts.Commands;

/// <summary>Gibt einem Konto eine Rolle oder nimmt sie weg. Nur Admins. Ein Admin nimmt sich die Rolle Admin nicht selbst.</summary>
public record SetRoleCommand(string AdminId, string UserId, string Role, bool Granted);

internal class SetRoleHandler(UserManager<User> users) : ICommandHandler<SetRoleCommand, AccountResult>
{
    public async Task<AccountResult> Handle(SetRoleCommand command, CancellationToken ct = default)
    {
        var admin = await users.FindByIdAsync(command.AdminId);
        var user = await users.FindByIdAsync(command.UserId);
        var selfDemotion = command.AdminId == command.UserId && command.Role == Roles.Admin && !command.Granted;
        if (admin is null || user is null || !Roles.All.Contains(command.Role) || selfDemotion || !await users.IsInRoleAsync(admin, Roles.Admin))
        {
            return AccountResult.Fail(AccountError.Other);
        }

        var has = await users.IsInRoleAsync(user, command.Role);
        var result = (command.Granted, has) switch
        {
            (true, false) => await users.AddToRoleAsync(user, command.Role),
            (false, true) => await users.RemoveFromRoleAsync(user, command.Role),
            _ => IdentityResult.Success,
        };
        return AccountErrors.From(result);
    }
}
