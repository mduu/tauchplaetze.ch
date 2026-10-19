using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Mail.Commands;

namespace Tauchplaetze.Web.Features.Mail;

/// <summary>Ruft den Command `ProcessMailQueue` in einer Schleife. Ist die Queue leer, wartet er 5 s. Enthält keine Fachlogik (ADR-006, ADR-010).</summary>
internal class MailQueueWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<MailQueueWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Idle = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan AfterError = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var wait = Idle;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<ICommandHandler<ProcessMailQueueCommand, ProcessMailQueueResult>>()
                    .Handle(new ProcessMailQueueCommand(), stoppingToken);
                wait = result.Idle ? Idle : TimeSpan.Zero;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Mail queue processing failed");
                wait = AfterError;
            }

            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, time, stoppingToken);
            }
        }
    }
}

/// <summary>Prüft alle 15 Minuten die Dead-Letter-Queue über den Command `CheckDeadLetters` (erster Lauf nach 1 Minute).</summary>
internal class DeadLetterMonitorWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<DeadLetterMonitorWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), time, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ICommandHandler<CheckDeadLettersCommand, int>>().Handle(new CheckDeadLettersCommand(), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Deadletter check failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), time, stoppingToken);
        }
    }
}
