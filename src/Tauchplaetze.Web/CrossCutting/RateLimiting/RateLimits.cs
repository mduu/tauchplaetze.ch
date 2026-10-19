using System.Net;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Tauchplaetze.Web.Features.About;
using Tauchplaetze.Web.Features.Platform;

namespace Tauchplaetze.Web.CrossCutting.RateLimiting;

/// <summary>Alle Rate Limits der App mit gemeinsamer Erkennung des Clients und gemeinsamer Antwort bei Abweisung (`infrastructure.md`).</summary>
internal static class RateLimits
{
    public static IServiceCollection AddRateLimits(this IServiceCollection services, IConfiguration configuration) =>
        services.Configure<ContactRateLimitOptions>(configuration.GetSection(ContactRateLimitOptions.Section)).AddRateLimiter(options =>
        {
            ContactRateLimit.AddPolicy(options);
            ContentEndpoints.AddRateLimitPolicy(options);
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

    /// <summary>Endpoints unter `/api` bekommen 429, Formulare eine Umleitung mit Hinweis.</summary>
    private static ValueTask Reject(OnRejectedContext context, CancellationToken ct)
    {
        if (context.HttpContext.Request.Path.StartsWithSegments("/api"))
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return ValueTask.CompletedTask;
        }

        ContactRateLimit.Reject(context.HttpContext);
        return ValueTask.CompletedTask;
    }
}
