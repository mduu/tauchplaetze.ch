using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Forecasts.Calculation;

namespace Tauchplaetze.Core.Features.Forecasts.Queries;

/// <summary>Der Verlauf eines Platzes über die letzten <see cref="Days"/> Tage bis heute (`forecast.md`, «Verlauf»).</summary>
public record GetSiteIndexHistoryQuery(Guid DiveSiteId, int Days = 365);

internal class GetSiteIndexHistoryHandler(TauchplaetzeDbContext dbContext, TimeProvider time) : IQueryHandler<GetSiteIndexHistoryQuery, SiteIndexHistory>
{
    public async Task<SiteIndexHistory> Handle(GetSiteIndexHistoryQuery query, CancellationToken ct = default)
    {
        var today = SwissTime.Today(time);
        var first = today.AddDays(1 - query.Days);
        var rows = await dbContext.DailyForecasts
            .AsNoTracking()
            .Where(f => f.DiveSiteId == query.DiveSiteId && f.Date >= first && f.Date <= today)
            .OrderBy(f => f.Date)
            .Select(f => new { f.Date, f.DiveIndex, f.SecchiDepthShore, f.SecchiDepth, f.ClaritySource, f.WindBeaufort, f.WindGustsKmh, f.AlgaeLevel, f.ModelVersion })
            .ToListAsync(ct);

        var smoothed = History.Smooth([.. rows.Select(r => (r.Date, r.DiveIndex))]);
        var days = rows
            .Select((r, i) => new HistoryDay(r.Date, r.DiveIndex, smoothed[i], r.SecchiDepthShore, r.SecchiDepth, r.ClaritySource, r.WindBeaufort, r.WindGustsKmh, r.AlgaeLevel, r.ModelVersion))
            .ToList();
        return new SiteIndexHistory(days, RuleChanges.Of([.. days.Select(d => (d.Date, d.ModelVersion))]));
    }
}

/// <summary>Regelwechsel in einer Reihe: jeder Tag, an dem die `ModelVersion` anders ist als am Tag davor.</summary>
internal static class RuleChanges
{
    public static IReadOnlyList<RuleChange> Of(IReadOnlyList<(DateOnly Date, string ModelVersion)> days) =>
        days.Skip(1)
            .Where((d, i) => d.ModelVersion != days[i].ModelVersion)
            .Select(d => new RuleChange(d.Date, d.ModelVersion))
            .ToList();
}
