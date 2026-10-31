using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Forecasts.Commands;
using Tauchplaetze.Core.Features.Forecasts.Queries;

namespace Tauchplaetze.Web.Features.Forecasts;

/// <summary>Startet den Command `RunForecast` nach Zeitplan. Enthält keine Fachlogik (ADR-006).</summary>
internal class ForecastWorker(IServiceScopeFactory scopes, IOptions<ForecastSchedule> schedule, TimeProvider time, ILogger<ForecastWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (schedule.Value.RunOnStartup && !await RecentRunExists(stoppingToken))
        {
            await Run(stoppingToken);
        }
        else if (await AbortedRunToResume(stoppingToken))
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

    /// <remarks>Ist die Datenbank beim Start nicht erreichbar, läuft der Lauf trotzdem an. Er meldet den Fehler selbst.</remarks>
    private async Task<bool> RecentRunExists(CancellationToken ct)
    {
        try
        {
            return await LastRunIsRecent(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Last forecast run could not be read");
            return false;
        }
    }

    /// <summary>Ein Deploy oder Neustart brach den Lauf von heute ab: nachholen, ausser der nächste geplante Lauf kommt bald (`forecast.md`, «Abbruch und Nachholen»).</summary>
    private async Task<bool> AbortedRunToResume(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var state = await scope.ServiceProvider.GetRequiredService<IQueryHandler<GetForecastRunStateQuery, ForecastRunState>>()
                .Handle(new GetForecastRunStateQuery(), ct);
            if (state.ResumeAborted)
            {
                logger.LogInformation("Aborted forecast run of today is resumed");
            }

            return state.ResumeAborted;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Forecast run state could not be read");
            return false;
        }
    }

    private async Task<bool> LastRunIsRecent(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var last = await scope.ServiceProvider.GetRequiredService<IQueryHandler<GetLastForecastRunQuery, LastForecastRun?>>()
            .Handle(new GetLastForecastRunQuery(), ct);
        var recent = last is not null && last.StartedAt > time.GetUtcNow() - schedule.Value.StartupMinAge ? last : null;
        if (recent is not null)
        {
            logger.LogInformation("Startup forecast run skipped, last run {RunId} at {StartedAt}", recent.Id, recent.StartedAt);
        }

        return recent is not null;
    }

    private async Task Run(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ICommandHandler<RunForecastCommand, RunForecastResult>>()
                .Handle(new RunForecastCommand(), ct);
            if (result.Skipped)
            {
                logger.LogInformation("Forecast run skipped, another run holds the lock");
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Forecast run failed");
        }
    }
}
