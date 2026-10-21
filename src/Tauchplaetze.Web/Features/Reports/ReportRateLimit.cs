using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Tauchplaetze.Web.CrossCutting.Localization;
using Tauchplaetze.Web.CrossCutting.RateLimiting;

namespace Tauchplaetze.Web.Features.Reports;

/// <summary>Einstellungen des Rate Limits für «Tauchgang melden» (`Reports:RateLimit`, `infrastructure.md`).</summary>
internal class ReportRateLimitOptions
{
    public const string Section = "Reports:RateLimit";

    public int PerHour { get; set; } = 20;
}

/// <summary>
/// Begrenzt das Melden pro Client und Stunde, zusätzlich zur Grenze pro Konto im Core. Nur POST zählt.
/// Abgewiesen leitet auf dieselbe Seite mit `limit=1` um.
/// </summary>
internal static class ReportRateLimit
{
    public const string Policy = "report-form";

    public static void AddPolicy(RateLimiterOptions options) =>
        options.AddPolicy(Policy, context => HttpMethods.IsPost(context.Request.Method)
            ? RateLimitPartition.GetFixedWindowLimiter($"report:{RateLimits.ClientOf(context)}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = context.RequestServices.GetRequiredService<IOptions<ReportRateLimitOptions>>().Value.PerHour,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
            })
            : RateLimitPartition.GetNoLimiter("read"));

    public static bool Handles(HttpRequest request) =>
        request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) is [_, _, _, var last] && PublicRoutes.Report.Values.Contains(last);

    public static void Reject(HttpContext context)
    {
        var request = context.Request;
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = $"{request.PathBase}{request.Path}?limit=1";
    }
}
