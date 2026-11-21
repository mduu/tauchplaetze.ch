using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Forecasts.Commands;

namespace Tauchplaetze.Web.Features.Forecasts;

/// <summary>Startet den Command `ImportLakeLevels` beim Start und danach jede Stunde. Enthält keine Fachlogik (ADR-006).</summary>
internal class LakeLevelWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<LakeLevelWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        do
        {
            await Run(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task Run(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ICommandHandler<ImportLakeLevelsCommand, ImportLakeLevelsResult>>()
                .Handle(new ImportLakeLevelsCommand(), ct);
            logger.LogInformation("Lake level import: {Stations} stations, {Missing} without values, {Norms} new norms, failed {Failed}, skipped {Skipped}",
                result.Stations, result.StationsWithoutValues, result.Norms, result.Failed, result.Skipped);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Lake level import failed");
        }
    }
}
