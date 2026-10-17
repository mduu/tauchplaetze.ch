using Microsoft.AspNetCore.Identity;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Accounts.Commands;

/// <summary>Ein Admin aus der Konfiguration (`Accounts:Admins`).</summary>
public record AdminAccount(string Email, string DisplayName);

/// <summary>Legt fehlende Admin-Konten an und gibt allen die Rolle Admin. Ein zweiter Lauf ändert nichts.</summary>
public record EnsureAdminsCommand(IReadOnlyList<AdminAccount> Admins);

/// <summary>Die Konto-IDs der Admins, in der Reihenfolge der Konfiguration.</summary>
public record EnsureAdminsResult(IReadOnlyList<string> UserIds);

internal class EnsureAdminsHandler(UserManager<User> users) : ICommandHandler<EnsureAdminsCommand, EnsureAdminsResult>
{
    public async Task<EnsureAdminsResult> Handle(EnsureAdminsCommand command, CancellationToken ct = default)
    {
        var ids = new List<string>();

        foreach (var admin in command.Admins)
        {
            var user = await users.FindByEmailAsync(admin.Email) ?? await Create(admin);

            if (!await users.IsInRoleAsync(user, Roles.Admin))
            {
                Ensure(await users.AddToRoleAsync(user, Roles.Admin));
            }

            ids.Add(user.Id);
        }

        return new EnsureAdminsResult(ids);
    }

    private async Task<User> Create(AdminAccount admin)
    {
        var user = new User
        {
            UserName = admin.Email,
            Email = admin.Email,
            EmailConfirmed = true,
            DisplayName = admin.DisplayName,
        };
        Ensure(await users.CreateAsync(user));
        return user;
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Admin-Konto: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
