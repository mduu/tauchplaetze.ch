using Microsoft.AspNetCore.Identity;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Accounts.Commands;

/// <summary>Setzt ein neues Passwort mit dem Token aus der Mail.</summary>
public record ResetPasswordCommand(string UserId, string Token, string NewPassword);

internal class ResetPasswordHandler(UserManager<User> users) : ICommandHandler<ResetPasswordCommand, AccountResult>
{
    public async Task<AccountResult> Handle(ResetPasswordCommand command, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(command.UserId);
        var token = AccountLinks.DecodeToken(command.Token);
        if (user is null || token is null)
        {
            return AccountResult.Fail(AccountError.InvalidToken);
        }

        var result = AccountErrors.From(await users.ResetPasswordAsync(user, token, command.NewPassword));
        if (result.Succeeded)
        {
            await users.SetLockoutEndDateAsync(user, null);
        }

        return result;
    }
}
