using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Accounts.Commands;

/// <summary>
/// Schickt einen Link für ein neues Passwort, wenn es ein bestätigtes Konto gibt. Das Ergebnis verrät nicht, ob es das Konto gibt.
/// <paramref name="ResetPathTemplate"/> ist ein Pfad der Website mit `{user}` und `{token}`.
/// </summary>
public record RequestPasswordResetCommand(string Email, string ResetPathTemplate);

internal class RequestPasswordResetHandler(UserManager<User> users, MailDispatcher mail, IOptions<PublicSiteOptions> site) : ICommandHandler<RequestPasswordResetCommand, Unit>
{
    public async Task<Unit> Handle(RequestPasswordResetCommand command, CancellationToken ct = default)
    {
        var user = await users.FindByEmailAsync(command.Email.Trim());
        if (user is { EmailConfirmed: true, BlockedAt: null, Email: { } email })
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            await mail.SendNow(email, user.PreferredCulture, new ResetPasswordMail(user.DisplayName, site.Value.Link(AccountLinks.Build(command.ResetPathTemplate, user.Id, token))), ct);
        }

        return Unit.Value;
    }
}
