using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Accounts.Commands;

/// <summary>
/// Schickt einem Konto den Link für ein neues Passwort, ausgelöst von einem Admin. Dieselbe Mail wie «Passwort vergessen».
/// Nur für bestätigte, nicht gesperrte Konten. <paramref name="ResetPathTemplate"/> ist ein Pfad der Website mit `{user}` und `{token}`.
/// </summary>
public record SendPasswordResetCommand(string AdminId, string AccountId, string ResetPathTemplate);

internal class SendPasswordResetHandler(UserManager<User> users, AccountRoles roles, MailDispatcher mail, IOptions<PublicSiteOptions> site) : ICommandHandler<SendPasswordResetCommand, AccountResult>
{
    public async Task<AccountResult> Handle(SendPasswordResetCommand command, CancellationToken ct = default)
    {
        var (user, error) = await AdminAccounts.Target(users, roles, command.AdminId, command.AccountId);
        if (user is null)
        {
            return AccountResult.Fail(error!.Value);
        }

        if (user is not { EmailConfirmed: true, BlockedAt: null, Email: { } email })
        {
            return AccountResult.Fail(AccountError.NotAllowed);
        }

        var token = await users.GeneratePasswordResetTokenAsync(user);
        await mail.SendNow(email, user.PreferredCulture, new ResetPasswordMail(user.DisplayName, site.Value.Link(AccountLinks.Build(command.ResetPathTemplate, user.Id, token))), ct);
        return AccountResult.Ok;
    }
}
