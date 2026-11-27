using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Susv.Commands;

/// <summary>«Jetzt abgleichen» auf `/admin/susv`. Nur Admins, die nicht gesperrt sind. Läuft schon ein Import, startet nichts (Lock).</summary>
public record StartSusvImportCommand(string AdminId);

/// <summary>Ergebnis eines Starts von Hand. Mit <see cref="Error"/> lief nichts.</summary>
public record StartSusvImportResult(AccountError? Error, ImportSusvEntriesResult? Import);

internal class StartSusvImportHandler(AccountRoles roles, ICommandHandler<ImportSusvEntriesCommand, ImportSusvEntriesResult> import)
    : ICommandHandler<StartSusvImportCommand, StartSusvImportResult>
{
    public async Task<StartSusvImportResult> Handle(StartSusvImportCommand command, CancellationToken ct = default)
    {
        var rights = await roles.Of(command.AdminId);
        if (!rights.CanAdminister)
        {
            return new StartSusvImportResult(rights.IsBlocked ? AccountError.Blocked : AccountError.NotAllowed, null);
        }

        return new StartSusvImportResult(null, await import.Handle(new ImportSusvEntriesCommand(), ct));
    }
}
