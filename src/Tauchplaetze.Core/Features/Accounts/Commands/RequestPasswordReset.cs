using Microsoft.AspNetCore.Identity;
using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Accounts.Commands;

/// <summary>
/// Schickt einen Link für ein neues Passwort, wenn es ein bestätigtes Konto gibt. Das Ergebnis verrät nicht, ob es das Konto gibt.
/// <paramref name="ResetLinkTemplate"/> enthält `{user}` und `{token}`.
/// </summary>
public record RequestPasswordResetCommand(string Email, string ResetLinkTemplate);

internal class RequestPasswordResetHandler(UserManager<User> users, IMailSender mail) : ICommandHandler<RequestPasswordResetCommand, Unit>
{
    public async Task<Unit> Handle(RequestPasswordResetCommand command, CancellationToken ct = default)
    {
        var user = await users.FindByEmailAsync(command.Email.Trim());
        if (user is { EmailConfirmed: true, BlockedAt: null, Email: { } email })
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            await mail.Send(AccountMails.Create(email, user.PreferredCulture, "Reset", user.DisplayName, AccountLinks.Build(command.ResetLinkTemplate, user.Id, token)), ct);
        }

        return Unit.Value;
    }
}
