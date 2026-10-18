using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Forecasts.Commands;

/// <summary>
/// Startet einen Prognose-Lauf von Hand aus dem CMS (`cms.md`, «Prognose-Status»). Nur Admins, die nicht gesperrt sind.
/// Der Worker ruft <see cref="RunForecastCommand"/> direkt.
/// </summary>
public record StartForecastRunCommand(string AdminId);

internal class StartForecastRunHandler(AccountRoles roles, ICommandHandler<RunForecastCommand, RunForecastResult> run)
    : ICommandHandler<StartForecastRunCommand, StartForecastRunResult>
{
    public async Task<StartForecastRunResult> Handle(StartForecastRunCommand command, CancellationToken ct = default)
    {
        var rights = await roles.Of(command.AdminId);
        if (!rights.CanAdminister)
        {
            return new StartForecastRunResult(rights.IsBlocked ? AccountError.Blocked : AccountError.NotAllowed, null);
        }

        return new StartForecastRunResult(null, await run.Handle(new RunForecastCommand(), ct));
    }
}

/// <summary>Ergebnis eines Starts von Hand. Mit <see cref="Error"/> lief nichts.</summary>
public record StartForecastRunResult(AccountError? Error, RunForecastResult? Run);
