using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Media.Commands;
using Tauchplaetze.Web.CrossCutting.Jobs;

namespace Tauchplaetze.Web.Features.Media;

/// <summary>Startet täglich um 03:30 Lokalzeit den Command `PurgeUnusedMedia`. Enthält keine Fachlogik (ADR-006).</summary>
internal class MediaPurgeWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<MediaPurgeWorker> logger)
    : DailyWorker(new TimeOnly(3, 30), scopes, time, logger)
{
    protected override async Task Run(IServiceProvider services, CancellationToken ct)
    {
        var result = await services.GetRequiredService<ICommandHandler<PurgeUnusedMediaCommand, PurgeUnusedMediaResult>>()
            .Handle(new PurgeUnusedMediaCommand(), ct);
        logger.LogInformation("Media purge: {Deleted} photos deleted, skipped {Skipped}", result.Deleted, result.Skipped);
    }
}
