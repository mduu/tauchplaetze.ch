using Microsoft.AspNetCore.Identity;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Accounts.Commands;

/// <summary>Gibt einem Konto eine Rolle oder nimmt sie weg. Nur Admins, die nicht gesperrt sind. Ein Admin nimmt sich die Rolle Admin nicht selbst. Der letzte aktive Admin behält sie.</summary>
public record SetRoleCommand(string AdminId, string UserId, string Role, bool Granted);

internal class SetRoleHandler(UserManager<User> users, AccountRoles roles) : ICommandHandler<SetRoleCommand, AccountResult>
{
    public async Task<AccountResult> Handle(SetRoleCommand command, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(command.UserId);
        if (user is null || !Roles.All.Contains(command.Role))
        {
            return AccountResult.Fail(AccountError.Other);
        }

        var admin = await roles.Of(command.AdminId);
        if (admin.IsBlocked)
        {
            return AccountResult.Fail(AccountError.Blocked);
        }

        var removesAdmin = command.Role == Roles.Admin && !command.Granted;
        if (!admin.CanAdminister || (removesAdmin && command.AdminId == command.UserId))
        {
            return AccountResult.Fail(AccountError.NotAllowed);
        }

        if (removesAdmin && await roles.IsLastActiveAdmin(user.Id))
        {
            return AccountResult.Fail(AccountError.LastAdmin);
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
