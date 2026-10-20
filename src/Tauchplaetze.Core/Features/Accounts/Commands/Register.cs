using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Accounts.Commands;

/// <summary>
/// Selbstregistrierung als Mitwirkende (`cms.md`, «Rollen»). Schickt eine Mail mit Link zur Bestätigung.
/// Ist die Adresse schon registriert, ist das Ergebnis dasselbe wie bei einem neuen Konto, und die Mail sagt «Du hast schon ein Konto» (keine Konto-Enumeration).
/// <paramref name="ConfirmPathTemplate"/> ist ein Pfad der Website mit `{user}` und `{token}`, <paramref name="SignInPath"/> der Pfad der Anmeldung.
/// </summary>
public record RegisterCommand(string Email, string Password, string DisplayName, string Culture, string ConfirmPathTemplate, string SignInPath = "/de/account/sign-in");

internal class RegisterHandler(UserManager<User> users, MailDispatcher mail, IOptions<AccountsOptions> options, IOptions<PublicSiteOptions> site, TimeProvider time, AccountMailCooldown cooldown) : ICommandHandler<RegisterCommand, AccountResult>
{
    public async Task<AccountResult> Handle(RegisterCommand command, CancellationToken ct = default)
    {
        if (!options.Value.RegistrationOpen)
        {
            return AccountResult.Fail(AccountError.RegistrationClosed);
        }

        if (await users.FindByEmailAsync(command.Email.Trim()) is { Email: { } known } existing)
        {
            if (cooldown.TryStart(existing.Id, AccountMailCooldown.Registration))
            {
                await mail.SendNow(known, existing.PreferredCulture, new AccountExistsMail(existing.DisplayName, site.Value.Link(command.SignInPath)), ct);
            }

            return AccountResult.Ok;
        }

        var displayName = command.DisplayName.Trim();
        if (AccountErrors.DisplayName(displayName) is { } invalid)
        {
            return AccountResult.Fail(invalid);
        }

        if (await users.Users.AnyAsync(u => u.DisplayName == displayName, ct))
        {
            return AccountResult.Fail(AccountError.DisplayNameTaken);
        }

        var user = new User { UserName = command.Email.Trim(), Email = command.Email.Trim(), DisplayName = displayName, PreferredCulture = command.Culture, CreatedAt = time.GetUtcNow() };
        var created = AccountErrors.From(await users.CreateAsync(user, command.Password));
        if (!created.Succeeded)
        {
            return created;
        }

        await users.AddToRoleAsync(user, Roles.Contributor);
        cooldown.TryStart(user.Id, AccountMailCooldown.Registration);
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        await mail.SendNow(user.Email, user.PreferredCulture, new ConfirmEmailMail(displayName, site.Value.Link(AccountLinks.Build(command.ConfirmPathTemplate, user.Id, token))), ct);
        return AccountResult.Ok;
    }
}
