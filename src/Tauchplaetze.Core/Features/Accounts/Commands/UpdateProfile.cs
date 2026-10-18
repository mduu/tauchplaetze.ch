using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Accounts.Commands;

/// <summary>Ändert Anzeigename, Einwilligung zur öffentlichen Anzeige und Sprache (`cms.md`, «Datenschutz in der History»).</summary>
public record UpdateProfileCommand(string UserId, string DisplayName, bool ShowNamePublicly, string Culture);

internal class UpdateProfileHandler(UserManager<User> users) : ICommandHandler<UpdateProfileCommand, AccountResult>
{
    public async Task<AccountResult> Handle(UpdateProfileCommand command, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(command.UserId);
        if (user is null)
        {
            return AccountResult.Fail(AccountError.Other);
        }

        var displayName = command.DisplayName.Trim();
        if (AccountErrors.DisplayName(displayName) is { } invalid)
        {
            return AccountResult.Fail(invalid);
        }

        if (await users.Users.AnyAsync(u => u.DisplayName == displayName && u.Id != user.Id, ct))
        {
            return AccountResult.Fail(AccountError.DisplayNameTaken);
        }

        user.DisplayName = displayName;
        user.ShowNamePublicly = command.ShowNamePublicly;
        user.PreferredCulture = command.Culture;
        return AccountErrors.From(await users.UpdateAsync(user));
    }
}
