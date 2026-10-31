using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;

namespace Tauchplaetze.Core.Features.Forecasts.Queries;

/// <summary>Läuft gerade ein Prognose-Lauf, und muss ein abgebrochener Lauf von heute nachgeholt werden (`forecast.md`, «Abbruch und Nachholen»)?</summary>
public record GetForecastRunStateQuery;

/// <summary>
/// <see cref="Running"/>: Ein Lauf steht auf «läuft» und ist jünger als <see cref="ForecastRunTimes.AbortedAfter"/>. Der Deploy wartet darauf.
/// <see cref="ResumeAborted"/>: Der letzte Lauf von heute brach ab, kein späterer Lauf kam durch, und der nächste geplante Lauf ist mehr als <see cref="ForecastRunTimes.ResumeGap"/> entfernt.
/// </summary>
public record ForecastRunState(bool Running, bool ResumeAborted);

/// <summary>Zeiten rund um abgebrochene Läufe.</summary>
public static class ForecastRunTimes
{
    /// <summary>Ein Lauf, der länger auf «läuft» steht, gilt als abgebrochen. Ein voller Lauf dauert rund 25 Minuten.</summary>
    public static readonly TimeSpan AbortedAfter = TimeSpan.FromHours(2);

    /// <summary>Kommt der nächste geplante Lauf früher, holt der Start keinen Lauf nach.</summary>
    public static readonly TimeSpan ResumeGap = TimeSpan.FromHours(1);
}

internal class GetForecastRunStateHandler(TauchplaetzeDbContext dbContext, IOptions<ForecastSchedule> schedule, TimeProvider time)
    : IQueryHandler<GetForecastRunStateQuery, ForecastRunState>
{
    public async Task<ForecastRunState> Handle(GetForecastRunStateQuery query, CancellationToken ct = default)
    {
        var now = time.GetUtcNow();
        var runningSince = now - ForecastRunTimes.AbortedAfter;
        var running = await dbContext.ForecastRuns.AsNoTracking()
            .AnyAsync(r => r.Status == ForecastRunStatus.Running && r.StartedAt > runningSince, ct);

        return new ForecastRunState(running, !running && await LastRunTodayAborted(now, ct) && schedule.Value.NextRun(now) - now > ForecastRunTimes.ResumeGap);
    }

    private async Task<bool> LastRunTodayAborted(DateTimeOffset now, CancellationToken ct)
    {
        var startOfToday = SwissTime.ToUtc(SwissTime.DayOf(now), 0);
        var last = await dbContext.ForecastRuns.AsNoTracking()
            .Where(r => r.StartedAt >= startOfToday)
            .OrderByDescending(r => r.StartedAt)
            .Select(r => (ForecastRunStatus?)r.Status)
            .FirstOrDefaultAsync(ct);

        return last is ForecastRunStatus.Aborted or ForecastRunStatus.Running;
    }
}
