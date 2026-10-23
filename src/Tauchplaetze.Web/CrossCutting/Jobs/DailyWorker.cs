namespace Tauchplaetze.Web.CrossCutting.Jobs;

/// <summary>Startet einmal am Tag zur Lokalzeit <paramref name="runAt"/> (Schweiz) einen Command. Ein Fehler landet im Log, der nächste Tag läuft trotzdem.</summary>
internal abstract class DailyWorker(TimeOnly runAt, IServiceScopeFactory scopes, TimeProvider time, ILogger logger) : BackgroundService
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var next = NextRun(time.GetUtcNow());
            await Task.Delay(next - time.GetUtcNow(), time, stoppingToken);
            await RunGuarded(stoppingToken);
        }
    }

    /// <summary>Ruft den Command in einem eigenen Scope auf und schreibt das Ergebnis ins Log.</summary>
    protected abstract Task Run(IServiceProvider services, CancellationToken ct);

    private DateTimeOffset NextRun(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Zone).DateTime);
        return Enumerable.Range(0, 2)
            .Select(offset => today.AddDays(offset).ToDateTime(runAt))
            .Select(candidate => new DateTimeOffset(candidate, Zone.GetUtcOffset(candidate)))
            .First(candidate => candidate > now);
    }

    private async Task RunGuarded(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await Run(scope.ServiceProvider, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "{Job} failed", GetType().Name);
        }
    }
}
