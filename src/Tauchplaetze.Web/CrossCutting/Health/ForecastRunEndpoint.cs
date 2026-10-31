using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Forecasts.Queries;

namespace Tauchplaetze.Web.CrossCutting.Health;

/// <summary>
/// `/health/run`: «running», solange ein Prognose-Lauf läuft, sonst «idle». Der Deploy wartet darauf, damit er keinen Lauf abbricht
/// (`infrastructure.md`, «CI/CD»). Öffentlich, ohne Details.
/// </summary>
internal static class ForecastRunEndpoint
{
    public const string Path = "/health/run";

    public static void Map(WebApplication app) =>
        app.MapGet(Path, async (HttpContext context, IQueryHandler<GetForecastRunStateQuery, ForecastRunState> state, CancellationToken ct) =>
        {
            var running = (await state.Handle(new GetForecastRunStateQuery(), ct)).Running;
            context.Response.Headers.CacheControl = "no-store";
            return Results.Text(running ? "running" : "idle", "text/plain");
        });
}
