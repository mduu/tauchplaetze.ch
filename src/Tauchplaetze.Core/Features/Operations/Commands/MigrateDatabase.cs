using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;

namespace Tauchplaetze.Core.Features.Operations.Commands;

/// <summary>Bringt die Datenbank auf den Stand der letzten Migration.</summary>
public record MigrateDatabaseCommand;

internal class MigrateDatabaseHandler(TauchplaetzeDbContext dbContext) : ICommandHandler<MigrateDatabaseCommand, Unit>
{
    public async Task<Unit> Handle(MigrateDatabaseCommand command, CancellationToken ct = default)
    {
        await dbContext.Database.MigrateAsync(ct);
        return Unit.Value;
    }
}
