using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Content;
using Tauchplaetze.Core.Features.Content.Queries;
using Tauchplaetze.Web.CrossCutting.RateLimiting;
using Tauchplaetze.Web.Features.Accounts;

namespace Tauchplaetze.Web.Features.Platform;

/// <summary>Download des Inhaltspakets (ADR-011): für Admins über die Seite, für den Abruf von lokal über den Schlüssel.</summary>
internal static class ContentEndpoints
{
    public const string ExportPath = "/platform/inhalt/export";

    private const string RateLimitPolicy = "content-export";

    /// <summary>Höchstens 10 Abrufe pro Stunde und Client für den Endpoint mit Schlüssel.</summary>
    public static void AddRateLimitPolicy(RateLimiterOptions options) =>
        options.AddPolicy(RateLimitPolicy, context =>
            RateLimitPartition.GetFixedWindowLimiter(RateLimits.ClientOf(context), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
            }));

    public static IEndpointRouteBuilder MapContentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(ExportPath, (HttpContext context, IQueryHandler<ExportContentQuery, ExportContentResult> export) =>
                Export(context, export, AccountCookie.UserId(context.User) ?? "", null))
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        endpoints.MapGet($"/{ContentOptions.ExportPath}", (HttpContext context, IQueryHandler<ExportContentQuery, ExportContentResult> export) =>
                Export(context, export, null, context.Request.Headers[ContentOptions.KeyHeader].FirstOrDefault() ?? ""))
            .RequireRateLimiting(RateLimitPolicy);

        return endpoints;
    }

    /// <remarks>Das ZIP geht direkt in die Antwort. So kommen die ersten Bytes sofort, und Cloudflare bricht ein grosses Paket nicht ab.</remarks>
    private static async Task<IResult> Export(HttpContext context, IQueryHandler<ExportContentQuery, ExportContentResult> export, string? adminId, string? key)
    {
        var result = await export.Handle(new ExportContentQuery(adminId, key, createdAt => StartDownload(context.Response, createdAt)), context.RequestAborted);
        return result.Error switch
        {
            null => Results.Empty,
            ContentError.Disabled => Results.NotFound(),
            ContentError.InvalidKey => Results.Unauthorized(),
            _ => Results.StatusCode(StatusCodes.Status403Forbidden),
        };
    }

    private static Stream StartDownload(HttpResponse response, DateTimeOffset createdAt)
    {
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "application/zip";
        response.Headers.CacheControl = "private, no-store";
        response.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileNameStar = $"tauchplaetze-inhalt-{createdAt:yyyy-MM-dd-HHmm}.zip",
        }.ToString();
        return response.Body;
    }
}
