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
        if (await roles.Of(command.AdminId) is not { IsAdmin: true, IsBlocked: false })
        {
            return new StartForecastRunResult(false, null);
        }

        return new StartForecastRunResult(true, await run.Handle(new RunForecastCommand(), ct));
    }
}

/// <summary>Ergebnis eines Starts von Hand. Ohne Recht läuft nichts.</summary>
public record StartForecastRunResult(bool Allowed, RunForecastResult? Run);
