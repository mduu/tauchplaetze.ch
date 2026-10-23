using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;

namespace Tauchplaetze.Core.Features.Accounts.Commands;

/// <summary>
/// Löscht Konten, deren Adresse <see cref="AfterDays"/> Tage nach der Registrierung noch unbestätigt ist (`cms.md`, «Rollen»).
/// Solche Konten können sich nie anmelden; meist sind es Bots oder Tippfehler. Konten ohne Datum der Registrierung bleiben.
/// Ein zweiter Lauf findet nichts mehr.
/// </summary>
public record DeleteUnconfirmedAccountsCommand
{
    public const int AfterDays = 7;
}

/// <summary>Ergebnis: Zahl der gelöschten Konten.</summary>
public record DeleteUnconfirmedAccountsResult(int Deleted);

internal class DeleteUnconfirmedAccountsHandler(TauchplaetzeDbContext dbContext, AccountDeletion deletion, TimeProvider time)
    : ICommandHandler<DeleteUnconfirmedAccountsCommand, DeleteUnconfirmedAccountsResult>
{
    public async Task<DeleteUnconfirmedAccountsResult> Handle(DeleteUnconfirmedAccountsCommand command, CancellationToken ct = default)
    {
        var cutoff = time.GetUtcNow().AddDays(-DeleteUnconfirmedAccountsCommand.AfterDays);
        var stale = await dbContext.Users.Where(u => !u.EmailConfirmed && u.CreatedAt != null && u.CreatedAt < cutoff).ToListAsync(ct);
        var deleted = 0;
        foreach (var user in stale)
        {
            if ((await deletion.Delete(user, ct)).Succeeded)
            {
                deleted++;
            }
        }

        return new DeleteUnconfirmedAccountsResult(deleted);
    }
}
