using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Forecasts.Commands;

namespace Tauchplaetze.Web.Features.Forecasts;

/// <summary>Startet den Command `ImportDischarge` beim Start und danach alle 30 Minuten. Enthält keine Fachlogik (ADR-006).</summary>
internal class DischargeWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<DischargeWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

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
            var result = await scope.ServiceProvider.GetRequiredService<ICommandHandler<ImportDischargeCommand, ImportDischargeResult>>()
                .Handle(new ImportDischargeCommand(), ct);
            logger.LogInformation("Discharge import: {Stations} stations, {Missing} without values, failed {Failed}, skipped {Skipped}",
                result.Stations, result.StationsWithoutValues, result.Failed, result.Skipped);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Discharge import failed");
        }
    }
}
