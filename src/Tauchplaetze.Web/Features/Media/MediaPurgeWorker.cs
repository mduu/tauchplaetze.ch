using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Media.Commands;

namespace Tauchplaetze.Web.Features.Media;

/// <summary>Startet täglich um 03:30 Lokalzeit den Command `PurgeUnusedMedia`. Enthält keine Fachlogik (ADR-006).</summary>
internal class MediaPurgeWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<MediaPurgeWorker> logger) : BackgroundService
{
    private static readonly TimeOnly RunAt = new(3, 30);

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var next = NextRun(time.GetUtcNow());
            await Task.Delay(next - time.GetUtcNow(), time, stoppingToken);
            await Run(stoppingToken);
        }
    }

    private static DateTimeOffset NextRun(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Zone).DateTime);
        return Enumerable.Range(0, 2)
            .Select(offset => today.AddDays(offset).ToDateTime(RunAt))
            .Select(candidate => new DateTimeOffset(candidate, Zone.GetUtcOffset(candidate)))
            .First(candidate => candidate > now);
    }

    private async Task Run(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ICommandHandler<PurgeUnusedMediaCommand, PurgeUnusedMediaResult>>()
                .Handle(new PurgeUnusedMediaCommand(), ct);
            logger.LogInformation("Media purge: {Deleted} photos deleted, skipped {Skipped}", result.Deleted, result.Skipped);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Media purge failed");
        }
    }
}
