using System.Net;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Tauchplaetze.Web.Features.About;
using Tauchplaetze.Web.Features.Accounts;
using Tauchplaetze.Web.Features.Platform;
using Tauchplaetze.Web.Features.Reports;

namespace Tauchplaetze.Web.CrossCutting.RateLimiting;

/// <summary>Alle Rate Limits der App mit gemeinsamer Erkennung des Clients und gemeinsamer Antwort bei Abweisung (`infrastructure.md`).</summary>
internal static class RateLimits
{
    public static IServiceCollection AddRateLimits(this IServiceCollection services, IConfiguration configuration) =>
        services.Configure<ContactRateLimitOptions>(configuration.GetSection(ContactRateLimitOptions.Section))
            .Configure<AccountFormsRateLimitOptions>(configuration.GetSection(AccountFormsRateLimitOptions.Section))
            .Configure<ReportRateLimitOptions>(configuration.GetSection(ReportRateLimitOptions.Section))
            .Configure<PageRateLimitOptions>(configuration.GetSection(PageRateLimitOptions.Section))
            .AddRateLimiter(options =>
        {
            options.GlobalLimiter = PageRateLimit.Limiter();
            ContactRateLimit.AddPolicy(options);
            ContentEndpoints.AddRateLimitPolicy(options);
            AccountFormsRateLimit.AddPolicy(options);
            ReportRateLimit.AddPolicy(options);
            options.OnRejected = Reject;
        });

    /// <summary>
    /// Der Client für das Zählen: die Adresse der Verbindung, `CF-Connecting-IP` nur mit <see cref="ContactRateLimitOptions.TrustCloudflareHeader"/>.
    /// IPv6 auf das /64-Netz gekürzt.
    /// </summary>
    public static string ClientOf(HttpContext context)
    {
        var trustCloudflare = context.RequestServices.GetRequiredService<IOptions<ContactRateLimitOptions>>().Value.TrustCloudflareHeader;
        var address = trustCloudflare && IPAddress.TryParse(context.Request.Headers["CF-Connecting-IP"].FirstOrDefault(), out var forwarded)
            ? forwarded
            : context.Connection.RemoteIpAddress;
        return address switch
        {
            null => "unknown",
            { AddressFamily: System.Net.Sockets.AddressFamily.InterNetworkV6 } v6 when !v6.IsIPv4MappedToIPv6 => Convert.ToHexString(v6.GetAddressBytes()[..8]),
            _ => address.MapToIPv4().ToString(),
        };
    }

    /// <summary>Endpoints unter `/api` und Seiten bekommen 429, Formulare eine Umleitung mit Hinweis auf derselben Seite.</summary>
    private static ValueTask Reject(OnRejectedContext context, CancellationToken ct)
    {
        if (context.HttpContext.Request.Path.StartsWithSegments("/api"))
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return ValueTask.CompletedTask;
        }

        if (PageRateLimit.Handles(context.HttpContext))
        {
            PageRateLimit.Reject(context.HttpContext);
            return ValueTask.CompletedTask;
        }

        if (AccountFormsRateLimit.Handles(context.HttpContext.Request))
        {
            AccountFormsRateLimit.Reject(context.HttpContext);
            return ValueTask.CompletedTask;
        }

        if (ReportRateLimit.Handles(context.HttpContext.Request))
        {
            ReportRateLimit.Reject(context.HttpContext);
            return ValueTask.CompletedTask;
        }

        ContactRateLimit.Reject(context.HttpContext);
        return ValueTask.CompletedTask;
    }
}
