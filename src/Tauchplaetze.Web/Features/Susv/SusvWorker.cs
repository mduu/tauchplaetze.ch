using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Susv.Commands;
using Tauchplaetze.Web.CrossCutting.Jobs;

namespace Tauchplaetze.Web.Features.Susv;

/// <summary>Startet täglich um 04:30 Lokalzeit den Command `ImportSusvEntries`. Enthält keine Fachlogik (ADR-006).</summary>
internal class SusvWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<SusvWorker> logger)
    : DailyWorker(new TimeOnly(4, 30), scopes, time, logger)
{
    protected override async Task Run(IServiceProvider services, CancellationToken ct)
    {
        var result = await services.GetRequiredService<ICommandHandler<ImportSusvEntriesCommand, ImportSusvEntriesResult>>()
            .Handle(new ImportSusvEntriesCommand(), ct);
        logger.LogInformation("SUSV import: {Entries} entries, {New} new, {Changed} changed, {Removed} removed, {FailedTopics} topics failed, locked {Locked}",
            result.Entries, result.New, result.Changed, result.Removed, result.FailedTopics, result.Locked);
    }
}
