using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Forecasts.Calculation;

namespace Tauchplaetze.Core.Features.Forecasts.Queries;

/// <summary>Der Median des Index eines Gewässers über alle gespeicherten Jahre, für «Jahr über Jahr» (`forecast.md`, «Verlauf»).</summary>
public record GetWaterBodyIndexHistoryQuery(int WaterBodyId);

internal class GetWaterBodyIndexHistoryHandler(TauchplaetzeDbContext dbContext) : IQueryHandler<GetWaterBodyIndexHistoryQuery, WaterBodyIndexHistory>
{
    public async Task<WaterBodyIndexHistory> Handle(GetWaterBodyIndexHistoryQuery query, CancellationToken ct = default)
    {
        var rows = await dbContext.WaterBodyDailyIndexes
            .AsNoTracking()
            .Where(i => i.WaterBodyId == query.WaterBodyId)
            .OrderBy(i => i.Date)
            .ToListAsync(ct);

        var smoothed = History.Smooth([.. rows.Select(r => (r.Date, (decimal?)r.MedianIndex))]);
        var years = rows
            .Select((r, i) => new IndexPoint(r.Date, r.MedianIndex, smoothed[i], r.Sites))
            .GroupBy(p => p.Date.Year)
            .Select(g => new IndexYear(g.Key, [.. g]))
            .ToList();
        return new WaterBodyIndexHistory(years, RuleChanges.Of([.. rows.Select(r => (r.Date, r.ModelVersion))]));
    }
}
