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
        if (await PasswordErrors(user, command.Password) is { Succeeded: false } weak)
        {
            return weak;
        }

        if (await users.FindByEmailAsync(user.Email) is { Email: { } known } existing)
        {
            await AnswerKnownAddress(existing, known, command, ct);
            return AccountResult.Ok;
        }

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

    private async Task<AccountResult> PasswordErrors(User user, string password)
    {
        var results = await Task.WhenAll(users.PasswordValidators.Select(v => v.ValidateAsync(users, user, password)));
        var errors = results.SelectMany(r => r.Errors).ToArray();
        return AccountErrors.From(errors.Length == 0 ? IdentityResult.Success : IdentityResult.Failed(errors));
    }

    /// <summary>
    /// Unbestätigt: die Bestätigung noch einmal, sonst «Du hast schon ein Konto». Der Hash gleicht die Arbeit des neuen Kontos an,
    /// damit die Antwortzeit eine bekannte Adresse nicht verrät. Gesendet wird sofort wie beim neuen Konto (ADR-010).
    /// </summary>
    private async Task AnswerKnownAddress(User existing, string email, RegisterCommand command, CancellationToken ct)
    {
        users.PasswordHasher.HashPassword(existing, command.Password);
        if (!cooldown.TryStart(existing.Id, AccountMailCooldown.Registration))
        {
            return;
        }

        if (existing.EmailConfirmed)
        {
            await mail.SendNow(email, existing.PreferredCulture, new AccountExistsMail(existing.DisplayName, site.Value.Link(command.SignInPath)), ct);
            return;
        }

        var token = await users.GenerateEmailConfirmationTokenAsync(existing);
        await mail.SendNow(email, existing.PreferredCulture, new ConfirmEmailMail(existing.DisplayName, site.Value.Link(AccountLinks.Build(command.ConfirmPathTemplate, existing.Id, token))), ct);
    }
}
