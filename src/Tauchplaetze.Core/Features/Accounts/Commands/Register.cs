using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Accounts.Commands;

/// <summary>
/// Selbstregistrierung als Mitwirkende (`cms.md`, «Rollen»). Schickt eine Mail mit Link zur Bestätigung.
/// <paramref name="ConfirmLinkTemplate"/> enthält `{user}` und `{token}`.
/// </summary>
public record RegisterCommand(string Email, string Password, string DisplayName, string Culture, string ConfirmLinkTemplate);

internal class RegisterHandler(UserManager<User> users, IMailSender mail, IOptions<AccountsOptions> options) : ICommandHandler<RegisterCommand, AccountResult>
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

        var user = new User { UserName = command.Email.Trim(), Email = command.Email.Trim(), DisplayName = displayName, PreferredCulture = command.Culture };
        var created = AccountErrors.From(await users.CreateAsync(user, command.Password));
        if (!created.Succeeded)
        {
            return created;
        }

        await users.AddToRoleAsync(user, Roles.Contributor);
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        await mail.Send(AccountMails.Create(user.Email, user.PreferredCulture, "Confirm", displayName, AccountLinks.Build(command.ConfirmLinkTemplate, user.Id, token)), ct);
        return AccountResult.Ok;
    }
}
