using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;

namespace Tauchplaetze.Core.Features.Forecasts.Queries;

/// <summary>Start und Status des letzten Laufs ohne Fehlerdetails, für den Zeitplan des Workers.</summary>
public record GetLastForecastRunQuery;

/// <summary>Der letzte Lauf, ohne Fehlerdetails. <c>null</c>, solange es keinen gab.</summary>
public record LastForecastRun(Guid Id, DateTimeOffset StartedAt, ForecastRunStatus Status);

internal class GetLastForecastRunHandler(TauchplaetzeDbContext dbContext) : IQueryHandler<GetLastForecastRunQuery, LastForecastRun?>
{
    public Task<LastForecastRun?> Handle(GetLastForecastRunQuery query, CancellationToken ct = default) =>
        dbContext.ForecastRuns.AsNoTracking()
            .Where(r => r.Status != ForecastRunStatus.Failed)
            .OrderByDescending(r => r.StartedAt)
            .Select(r => new LastForecastRun(r.Id, r.StartedAt, r.Status))
            .FirstOrDefaultAsync(ct);
}
