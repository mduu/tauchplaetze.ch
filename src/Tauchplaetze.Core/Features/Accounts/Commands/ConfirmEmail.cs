using Microsoft.AspNetCore.Identity;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Accounts.Commands;

/// <summary>Bestätigt die E-Mail-Adresse mit dem Token aus der Mail.</summary>
public record ConfirmEmailCommand(string UserId, string Token);

internal class ConfirmEmailHandler(UserManager<User> users) : ICommandHandler<ConfirmEmailCommand, AccountResult>
{
    public async Task<AccountResult> Handle(ConfirmEmailCommand command, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(command.UserId);
        var token = AccountLinks.DecodeToken(command.Token);
        if (user is null || token is null)
        {
            return AccountResult.Fail(AccountError.InvalidToken);
        }

        return AccountErrors.From(await users.ConfirmEmailAsync(user, token));
    }
}
