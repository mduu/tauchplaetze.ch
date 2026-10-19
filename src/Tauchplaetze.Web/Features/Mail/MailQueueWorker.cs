using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Mail.Commands;

namespace Tauchplaetze.Web.Features.Mail;

/// <summary>
/// Ruft den Command `ProcessMailQueue` in einer Schleife (ADR-006, ADR-010). Ist die Queue leer, wartet er zuerst 5 s und verdoppelt bis 30 s.
/// Enthält keine Fachlogik. Ein Fehler beendet die Schleife nie, sonst stoppte der Host.
/// </summary>
internal class MailQueueWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<MailQueueWorker> logger) : BackgroundService
{
    private static readonly TimeSpan MinIdle = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan MaxIdle = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CreateQueues(stoppingToken);
        var idle = MinIdle;
        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan wait;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<ICommandHandler<ProcessMailQueueCommand, ProcessMailQueueResult>>()
                    .Handle(new ProcessMailQueueCommand(), stoppingToken);
                wait = result.Idle ? idle : TimeSpan.Zero;
                idle = result.Idle ? TimeSpan.FromTicks(Math.Min(idle.Ticks * 2, MaxIdle.Ticks)) : MinIdle;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Mail queue processing failed");
                wait = MaxIdle;
            }

            if (wait > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(wait, time, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    /// <summary>Legt die Queues beim Start an. Scheitert das, versucht es der nächste Versand wieder.</summary>
    private async Task CreateQueues(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateMailQueuesCommand, Unit>>().Handle(new CreateMailQueuesCommand(), ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Mail queues could not be created");
        }
    }
}

/// <summary>Prüft alle 15 Minuten die Dead-Letter-Queue über den Command `CheckDeadLetters` (erster Lauf nach 1 Minute).</summary>
internal class DeadLetterMonitorWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<DeadLetterMonitorWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), time, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<ICommandHandler<CheckDeadLettersCommand, int>>().Handle(new CheckDeadLettersCommand(), stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(ex, "Deadletter check failed");
                }

                await Task.Delay(TimeSpan.FromMinutes(15), time, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
    }
}
