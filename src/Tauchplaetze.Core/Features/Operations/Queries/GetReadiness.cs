using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Forecasts;

namespace Tauchplaetze.Core.Features.Operations.Queries;

/// <summary>Ist die App bereit: Datenbank erreichbar und ein Prognose-Lauf jünger als <see cref="ReadinessRules.MaxForecastAge"/>.</summary>
public record GetReadinessQuery;

/// <summary>Ergebnis für `/health/ready`. <see cref="Problem"/> nennt den Grund, wenn die App nicht bereit ist.</summary>
public record Readiness(bool IsReady, string? Problem, DateTimeOffset? LastForecastRun);

/// <summary>Regeln für die Bereitschaft (infrastructure.md, Betrieb).</summary>
public static class ReadinessRules
{
    /// <summary>Zwei Läufe pro Tag. Fehlen mehr als ein Lauf und ein paar Stunden Reserve, stimmt etwas nicht.</summary>
    public static readonly TimeSpan MaxForecastAge = TimeSpan.FromHours(30);
}

internal class GetReadinessHandler(TauchplaetzeDbContext dbContext, TimeProvider time) : IQueryHandler<GetReadinessQuery, Readiness>
{
    public async Task<Readiness> Handle(GetReadinessQuery query, CancellationToken ct = default)
    {
        if (!await dbContext.Database.CanConnectAsync(ct))
        {
            return new Readiness(false, "database unreachable", null);
        }

        var last = await dbContext.ForecastRuns.AsNoTracking()
            .Where(r => r.Status == ForecastRunStatus.Succeeded || r.Status == ForecastRunStatus.PartiallySucceeded)
            .MaxAsync(r => (DateTimeOffset?)r.StartedAt, ct);

        return last switch
        {
            null => new Readiness(false, "no forecast run yet", null),
            var started when time.GetUtcNow() - started > ReadinessRules.MaxForecastAge => new Readiness(false, "last forecast run too old", started),
            var started => new Readiness(true, null, started),
        };
    }
}
