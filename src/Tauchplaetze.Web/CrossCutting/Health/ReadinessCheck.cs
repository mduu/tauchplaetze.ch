using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Operations.Queries;

namespace Tauchplaetze.Web.CrossCutting.Health;

/// <summary>
/// `/health/ready`: Datenbank erreichbar und ein Prognose-Lauf jünger als 30 h. Der Endpoint ist öffentlich.
/// Das Ergebnis gilt <see cref="CacheFor"/>, so treffen viele Aufrufe die Datenbank höchstens zweimal pro Minute.
/// </summary>
internal class ReadinessCheck(IQueryHandler<GetReadinessQuery, Readiness> readiness, ReadinessCheck.Cache cache, TimeProvider time) : IHealthCheck
{
    public const string Path = "/health/ready";

    private const string Tag = "ready";

    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        if (cache.Last is not { } last || now - last.CheckedAt > CacheFor)
        {
            last = (now, await readiness.Handle(new GetReadinessQuery(), cancellationToken));
            cache.Last = last;
        }

        return last.Result.IsReady ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy(last.Result.Problem);
    }

    public static void Register(IServiceCollection services)
    {
        services.AddSingleton<Cache>();
        services.AddHealthChecks().AddCheck<ReadinessCheck>(Tag, tags: [Tag]);
    }

    /// <remarks>`no-store`, damit kein Cache (Cloudflare, Browser) ein altes Ergebnis zeigt.</remarks>
    public static void Map(WebApplication app) =>
        app.MapHealthChecks(Path, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains(Tag),
            ResponseWriter = async (context, report) =>
            {
                context.Response.Headers.CacheControl = "no-store";
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync(report.Status.ToString());
            },
        });

    /// <summary>Das letzte Ergebnis, für alle Aufrufe gemeinsam.</summary>
    internal sealed class Cache
    {
        public (DateTimeOffset CheckedAt, Readiness Result)? Last { get; set; }
    }
}
