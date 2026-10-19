using System.Threading.RateLimiting;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Content;
using Tauchplaetze.Core.Features.Content.Queries;
using Tauchplaetze.Web.Features.Accounts;

namespace Tauchplaetze.Web.Features.Platform;

/// <summary>Download des Inhaltspakets (ADR-011): für Admins über die Seite, für den Abruf von lokal über den Schlüssel.</summary>
internal static class ContentEndpoints
{
    public const string ExportPath = "/platform/inhalt/export";

    public const string KeyExportPath = "/api/platform/content-export";

    public const string KeyHeader = "X-Content-Export-Key";

    private const string RateLimitPolicy = "content-export";

    /// <summary>Höchstens 10 Abrufe pro Stunde und Adresse für den Endpoint mit Schlüssel.</summary>
    public static IServiceCollection AddContentExportRateLimit(this IServiceCollection services) =>
        services.AddRateLimiter(options => options.AddPolicy(RateLimitPolicy, context =>
            RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
            })));

    public static IEndpointRouteBuilder MapContentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(ExportPath, (HttpContext context, IQueryHandler<ExportContentQuery, ExportContentResult> export) =>
                Export(context, export, AccountCookie.UserId(context.User) ?? "", null))
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        endpoints.MapGet(KeyExportPath, (HttpContext context, IQueryHandler<ExportContentQuery, ExportContentResult> export) =>
                Export(context, export, null, context.Request.Headers[KeyHeader].FirstOrDefault() ?? ""))
            .RequireRateLimiting(RateLimitPolicy);

        return endpoints;
    }

    /// <remarks>Das Paket entsteht in einer temporären Datei, die nach der Antwort verschwindet. Der ZIP-Writer schreibt synchron, die Antwort nicht.</remarks>
    private static async Task<IResult> Export(HttpContext context, IQueryHandler<ExportContentQuery, ExportContentResult> export, string? adminId, string? key)
    {
        var file = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        var result = await export.Handle(new ExportContentQuery(adminId, key, file), context.RequestAborted);
        if (result.Error is { } error)
        {
            await file.DisposeAsync();
            return error switch
            {
                ContentError.Disabled => Results.NotFound(),
                ContentError.InvalidKey => Results.Unauthorized(),
                _ => Results.StatusCode(StatusCodes.Status403Forbidden),
            };
        }

        file.Position = 0;
        context.Response.Headers.CacheControl = "private, no-store";
        return Results.File(file, "application/zip", $"tauchplaetze-inhalt-{DateTime.UtcNow:yyyy-MM-dd}.zip");
    }
}
