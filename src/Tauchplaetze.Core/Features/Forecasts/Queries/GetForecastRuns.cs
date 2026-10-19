using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Forecasts.Queries;

/// <summary>Die letzten Läufe des Prognose-Jobs mit Status und Fehlern pro Quelle (`cms.md`, «Prognose-Status»). Nur für Admins, sonst leer.</summary>
public record GetForecastRunsQuery(string AdminId, int Count = 10);

/// <summary>Ein Lauf mit den gescheiterten Calls.</summary>
public record ForecastRunSummary(Guid Id, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, ForecastRunStatus Status, int Calls, IReadOnlyList<string> Failures);

internal class GetForecastRunsHandler(TauchplaetzeDbContext dbContext, AccountRoles roles) : IQueryHandler<GetForecastRunsQuery, IReadOnlyList<ForecastRunSummary>>
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ForecastRunSummary>> Handle(GetForecastRunsQuery query, CancellationToken ct = default)
    {
        if (!(await roles.Of(query.AdminId)).CanAdminister)
        {
            return [];
        }

        var runs = await dbContext.ForecastRuns.AsNoTracking().OrderByDescending(r => r.StartedAt).Take(query.Count).ToListAsync(ct);
        return runs.Select(r =>
            {
                var calls = System.Text.Json.JsonSerializer.Deserialize<List<SourceCallStatus>>(r.SourceStatus, JsonOptions) ?? [];
                return new ForecastRunSummary(r.Id, r.StartedAt, r.FinishedAt, r.Status, calls.Count,
                    calls.Where(c => !c.Ok).Select(c => $"{c.Source}/{c.Key}: {c.StatusCode?.ToString() ?? "–"} {c.Error}").ToList());
            })
            .ToList();
    }
}
