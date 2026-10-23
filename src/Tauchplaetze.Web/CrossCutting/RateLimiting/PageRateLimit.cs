using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.Extensions.Options;

namespace Tauchplaetze.Web.CrossCutting.RateLimiting;

/// <summary>Einstellungen des Rate Limits für Seitenaufrufe (`Pages:RateLimit`, `infrastructure.md`).</summary>
internal class PageRateLimitOptions
{
    public const string Section = "Pages:RateLimit";

    /// <summary>Seiten pro Minute und Client im Dauerbetrieb. 0 schaltet das Limit aus.</summary>
    public int PerMinute { get; set; } = 60;

    /// <summary>So viele Seiten darf ein Client auf einmal holen, bevor <see cref="PerMinute"/> greift.</summary>
    public int Burst { get; set; } = 120;

    /// <summary>Seiten, die gleichzeitig rendern dürfen, über alle Clients. Weitere warten in der Queue.</summary>
    public int Concurrent { get; set; } = 4;

    public int QueueLimit { get; set; } = 200;
}

/// <summary>
/// Schützt die Datenbank vor Scrapern: Seitenaufrufe ohne Anmeldung pro Client als Token Bucket, dazu eine Grenze für gleichzeitige Seiten.
/// Zählt nur Seiten (Razor Components), keine statischen Dateien, Health Checks oder Endpoints. Angemeldete Konten zählen nicht.
/// </summary>
internal static class PageRateLimit
{
    private const string Global = "pages";

    public static PartitionedRateLimiter<HttpContext> Limiter() =>
        PartitionedRateLimiter.CreateChained(
            PartitionedRateLimiter.Create<HttpContext, string>(context => Counts(context, Settings(context)) is { } settings
                ? RateLimitPartition.GetTokenBucketLimiter($"page:{RateLimits.ClientOf(context)}", _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = settings.Burst,
                    TokensPerPeriod = settings.PerMinute,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                })
                : RateLimitPartition.GetNoLimiter("other")),
            PartitionedRateLimiter.Create<HttpContext, string>(context => Counts(context, Settings(context)) is { } settings
                ? RateLimitPartition.GetConcurrencyLimiter(Global, _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = settings.Concurrent,
                    QueueLimit = settings.QueueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                })
                : RateLimitPartition.GetNoLimiter("other")));

    private static PageRateLimitOptions Settings(HttpContext context) =>
        context.RequestServices.GetRequiredService<IOptions<PageRateLimitOptions>>().Value;

    public static bool Handles(HttpContext context) =>
        (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)) && IsPage(context);

    /// <summary>429 mit dem Hinweis, wann es wieder geht.</summary>
    public static void Reject(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = "60";
    }

    /// <summary>Die Einstellungen, wenn die Anfrage zählt, sonst null.</summary>
    private static PageRateLimitOptions? Counts(HttpContext context, PageRateLimitOptions settings) =>
        settings.PerMinute > 0 && Handles(context) && context.User.Identity?.IsAuthenticated != true ? settings : null;

    private static bool IsPage(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<ComponentTypeMetadata>() is not null;
}
