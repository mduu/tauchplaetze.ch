using Microsoft.AspNetCore.Identity;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Accounts.Commands;

/// <summary>
/// Löscht ein fremdes Konto, zum Beispiel ein Spam-Konto. Nur Admins, mit denselben Regeln wie die Selbstlöschung (<see cref="AccountDeletion"/>).
/// Das eigene Konto löscht ein Admin im Profil, nicht hier.
/// </summary>
public record DeleteAccountByAdminCommand(string AdminId, string AccountId);

internal class DeleteAccountByAdminHandler(UserManager<User> users, AccountRoles roles, AccountDeletion deletion) : ICommandHandler<DeleteAccountByAdminCommand, AccountResult>
{
    public async Task<AccountResult> Handle(DeleteAccountByAdminCommand command, CancellationToken ct = default)
    {
        if (command.AdminId == command.AccountId)
        {
            return AccountResult.Fail(AccountError.NotAllowed);
        }

        var (user, error) = await AdminAccounts.Target(users, roles, command.AdminId, command.AccountId);
        return user is null ? AccountResult.Fail(error!.Value) : await deletion.Delete(user, ct);
    }
}
