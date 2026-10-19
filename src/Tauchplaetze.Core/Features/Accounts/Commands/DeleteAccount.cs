using Microsoft.AspNetCore.Identity;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Accounts.Commands;

/// <summary>Löscht das eigene Konto (<see cref="AccountDeletion"/>).</summary>
public record DeleteAccountCommand(string UserId);

internal class DeleteAccountHandler(UserManager<User> users, AccountDeletion deletion) : ICommandHandler<DeleteAccountCommand, AccountResult>
{
    public async Task<AccountResult> Handle(DeleteAccountCommand command, CancellationToken ct = default) =>
        await users.FindByIdAsync(command.UserId) is { } user
            ? await deletion.Delete(user, ct)
            : AccountResult.Fail(AccountError.Other);
}
