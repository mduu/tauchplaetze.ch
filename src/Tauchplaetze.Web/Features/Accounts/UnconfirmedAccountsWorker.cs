using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Web.CrossCutting.Jobs;

namespace Tauchplaetze.Web.Features.Accounts;

/// <summary>Startet täglich um 03:45 Lokalzeit den Command `DeleteUnconfirmedAccounts`. Enthält keine Fachlogik (ADR-006).</summary>
internal class UnconfirmedAccountsWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<UnconfirmedAccountsWorker> logger)
    : DailyWorker(new TimeOnly(3, 45), scopes, time, logger)
{
    protected override async Task Run(IServiceProvider services, CancellationToken ct)
    {
        var result = await services.GetRequiredService<ICommandHandler<DeleteUnconfirmedAccountsCommand, DeleteUnconfirmedAccountsResult>>()
            .Handle(new DeleteUnconfirmedAccountsCommand(), ct);
        logger.LogInformation("Unconfirmed accounts: {Deleted} deleted", result.Deleted);
    }
}
