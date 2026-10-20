using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Operations.Queries;

namespace Tauchplaetze.Web.CrossCutting.Health;

/// <summary>`/health/ready`: Datenbank erreichbar und ein Prognose-Lauf jünger als 30 h. Weckt die Datenbank, darum nicht für den Health Check von App Service.</summary>
internal class ReadinessCheck(IQueryHandler<GetReadinessQuery, Readiness> readiness) : IHealthCheck
{
    public const string Path = "/health/ready";

    private const string Tag = "ready";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var result = await readiness.Handle(new GetReadinessQuery(), cancellationToken);
        return result.IsReady ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy(result.Problem);
    }

    public static void Register(IServiceCollection services) =>
        services.AddHealthChecks().AddCheck<ReadinessCheck>(Tag, tags: [Tag]);

    public static void Map(WebApplication app) =>
        app.MapHealthChecks(Path, new HealthCheckOptions { Predicate = r => r.Tags.Contains(Tag) });
}
