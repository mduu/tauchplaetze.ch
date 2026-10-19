using Microsoft.AspNetCore.Identity;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Accounts.Commands;

/// <summary>Bestätigt die E-Mail-Adresse eines Kontos von Hand, zum Beispiel wenn die Mail nicht ankam. Nur Admins.</summary>
public record ConfirmEmailManuallyCommand(string AdminId, string AccountId);

internal class ConfirmEmailManuallyHandler(UserManager<User> users, AccountRoles roles) : ICommandHandler<ConfirmEmailManuallyCommand, AccountResult>
{
    public async Task<AccountResult> Handle(ConfirmEmailManuallyCommand command, CancellationToken ct = default)
    {
        var (user, error) = await AdminAccounts.Target(users, roles, command.AdminId, command.AccountId);
        if (user is null)
        {
            return AccountResult.Fail(error!.Value);
        }

        if (user.EmailConfirmed)
        {
            return AccountResult.Ok;
        }

        user.EmailConfirmed = true;
        return AccountErrors.From(await users.UpdateAsync(user));
    }
}
