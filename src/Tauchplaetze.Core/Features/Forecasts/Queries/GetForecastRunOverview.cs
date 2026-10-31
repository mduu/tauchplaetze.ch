using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Forecasts.Queries;

/// <summary>
/// Übersicht der Prognose-Läufe für die Plattform (`cms.md`, «Prognose-Status»): Kacheln, eine Seite Läufe (neueste zuerst),
/// die Fehler gruppiert nach Quelle und Art. <paramref name="OnlyWithErrors"/>: nur Läufe mit Teilerfolg oder Fehler. Nur für Admins, sonst null.
/// </summary>
public record GetForecastRunOverviewQuery(string AdminId, bool OnlyWithErrors = false, int Page = 0, int PageSize = 20);

/// <summary>Kacheln und eine Seite Läufe. <see cref="NextRun"/>: der nächste Lauf nach Zeitplan.</summary>
public record ForecastRunOverview(
    ForecastRunRow? LastRun,
    DateTimeOffset NextRun,
    RunBalance Week,
    TimeSpan? AverageDuration,
    IReadOnlyList<ForecastRunRow> Runs,
    int TotalRuns);

/// <summary>Läufe der letzten 7 Tage nach Status.</summary>
public record RunBalance(int Total, int Succeeded, int PartiallySucceeded, int Failed);

/// <summary>Ein Lauf. <see cref="Progress"/> nur, solange er in diesem Prozess läuft.</summary>
public record ForecastRunRow(
    Guid Id,
    DateTimeOffset StartedAt,
    TimeSpan? Duration,
    ForecastRunStatus Status,
    int? Sites,
    int Calls,
    int FailedCalls,
    IReadOnlyList<FailureGroup> Failures,
    RunProgress? Progress);

internal class GetForecastRunOverviewHandler(
    TauchplaetzeDbContext dbContext,
    AccountRoles roles,
    ForecastProgress progress,
    IOptions<ForecastSchedule> schedule,
    TimeProvider time) : IQueryHandler<GetForecastRunOverviewQuery, ForecastRunOverview?>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ForecastRunOverview?> Handle(GetForecastRunOverviewQuery query, CancellationToken ct = default)
    {
        if (!(await roles.Of(query.AdminId)).CanAdminister)
        {
            return null;
        }

        var now = time.GetUtcNow();
        var weekStart = now.AddDays(-7);
        var week = await dbContext.ForecastRuns.AsNoTracking()
            .Where(r => r.StartedAt >= weekStart)
            .Select(r => new { r.Status, r.StartedAt, r.FinishedAt })
            .ToListAsync(ct);
        var durations = week.Where(r => r.FinishedAt is not null).Select(r => (r.FinishedAt!.Value - r.StartedAt).TotalSeconds).ToList();

        var runs = dbContext.ForecastRuns.AsNoTracking()
            .Where(r => !query.OnlyWithErrors || r.Status == ForecastRunStatus.PartiallySucceeded || r.Status == ForecastRunStatus.Failed || r.Status == ForecastRunStatus.Aborted);
        var total = await runs.CountAsync(ct);
        var page = await runs.OrderByDescending(r => r.StartedAt).Skip(query.Page * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        var last = await dbContext.ForecastRuns.AsNoTracking().OrderByDescending(r => r.StartedAt).FirstOrDefaultAsync(ct);

        return new ForecastRunOverview(
            last is null ? null : Row(last, now),
            schedule.Value.NextRun(now),
            new RunBalance(
                week.Count,
                week.Count(r => r.Status == ForecastRunStatus.Succeeded),
                week.Count(r => r.Status == ForecastRunStatus.PartiallySucceeded),
                week.Count(r => r.Status is ForecastRunStatus.Failed or ForecastRunStatus.Aborted)),
            durations.Count == 0 ? null : TimeSpan.FromSeconds(Math.Round(durations.Average())),
            [.. page.Select(r => Row(r, now))],
            total);
    }

    private ForecastRunRow Row(ForecastRun run, DateTimeOffset now)
    {
        var calls = JsonSerializer.Deserialize<List<SourceCallStatus>>(run.SourceStatus, JsonOptions) ?? [];
        var running = run.Status == ForecastRunStatus.Running ? progress.Of(run.Id) : null;
        var status = run.Status == ForecastRunStatus.Running && running is null && now - run.StartedAt > ForecastRunTimes.AbortedAfter ? ForecastRunStatus.Aborted : run.Status;
        return new ForecastRunRow(
            run.Id, run.StartedAt, run.FinishedAt - run.StartedAt, status, run.Sites,
            running?.Calls ?? calls.Count, calls.Count(c => !c.Ok), RunFailures.Group(calls), running);
    }
}
