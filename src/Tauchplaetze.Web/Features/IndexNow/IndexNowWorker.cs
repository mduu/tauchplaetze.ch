using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.IndexNow;
using Tauchplaetze.Core.Features.IndexNow.Commands;

namespace Tauchplaetze.Web.Features.IndexNow;

/// <summary>Startet den Command `SubmitIndexNow` im Intervall aus `IndexNow:Interval`. Enthält keine Fachlogik (ADR-006).</summary>
internal class IndexNowWorker(IServiceScopeFactory scopes, IOptions<IndexNowOptions> options, TimeProvider time, ILogger<IndexNowWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.Interval, time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await Run(stoppingToken);
        }
    }

    private async Task Run(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ICommandHandler<SubmitIndexNowCommand, SubmitIndexNowResult>>().Handle(new SubmitIndexNowCommand(), ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "IndexNow submission failed");
        }
    }
}
