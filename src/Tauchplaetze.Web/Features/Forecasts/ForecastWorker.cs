using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Forecasts.Commands;

namespace Tauchplaetze.Web.Features.Forecasts;

/// <summary>Startet den Command `RunForecast` nach Zeitplan. Enthält keine Fachlogik (ADR-006).</summary>
internal class ForecastWorker(IServiceScopeFactory scopes, IOptions<ForecastSchedule> schedule, TimeProvider time, ILogger<ForecastWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (schedule.Value.RunOnStartup)
        {
            await Run(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var next = schedule.Value.NextRun(time.GetUtcNow());
            logger.LogInformation("Next forecast run at {NextRun}", next);
            await Task.Delay(next - time.GetUtcNow(), time, stoppingToken);
            await Run(stoppingToken);
        }
    }

    private async Task Run(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ICommandHandler<RunForecastCommand, RunForecastResult>>()
                .Handle(new RunForecastCommand(), ct);
            logger.LogInformation("Forecast run {RunId}: {Status}, {Sites} sites, skipped {Skipped}", result.RunId, result.Status, result.Sites, result.Skipped);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Forecast run failed");
        }
    }
}
