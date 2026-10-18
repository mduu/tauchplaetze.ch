using Microsoft.AspNetCore.Identity;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Accounts.Commands;

/// <summary>Prüft E-Mail und Passwort. Zählt Fehlversuche (Lockout). Das Cookie setzt die Web-App.</summary>
public record SignInCommand(string Email, string Password);

/// <summary>Status und, bei Erfolg, das Konto.</summary>
public record SignInOutcome(SignInStatus Status, SignedInAccount? Account = null);

internal class SignInHandler(UserManager<User> users) : ICommandHandler<SignInCommand, SignInOutcome>
{
    public async Task<SignInOutcome> Handle(SignInCommand command, CancellationToken ct = default)
    {
        var user = await users.FindByEmailAsync(command.Email.Trim());
        if (user is null)
        {
            return new SignInOutcome(SignInStatus.Failed);
        }

        if (await users.IsLockedOutAsync(user))
        {
            return new SignInOutcome(SignInStatus.LockedOut);
        }

        if (!await users.CheckPasswordAsync(user, command.Password))
        {
            await users.AccessFailedAsync(user);
            return new SignInOutcome(await users.IsLockedOutAsync(user) ? SignInStatus.LockedOut : SignInStatus.Failed);
        }

        await users.ResetAccessFailedCountAsync(user);

        if (user.BlockedAt is not null)
        {
            return new SignInOutcome(SignInStatus.Blocked);
        }

        if (!user.EmailConfirmed)
        {
            return new SignInOutcome(SignInStatus.NotConfirmed);
        }

        var roles = await users.GetRolesAsync(user);
        return new SignInOutcome(SignInStatus.Succeeded, new SignedInAccount(user.Id, user.DisplayName, user.PreferredCulture, [.. roles]));
    }
}
