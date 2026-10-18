using System.Net;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace Tauchplaetze.Web.Features.About;

/// <summary>Einstellungen des Rate Limits für das Kontaktformular (`Contact`, `infrastructure.md`).</summary>
internal class ContactRateLimitOptions
{
    public const string Section = "Contact";

    public int MessagesPerHour { get; set; } = 5;

    /// <summary>
    /// Nur setzen, wenn die App ausschliesslich über Cloudflare erreichbar ist (Ingress auf die IP-Bereiche von Cloudflare beschränkt).
    /// Sonst kann jede Anfrage den Header fälschen.
    /// </summary>
    public bool TrustCloudflareHeader { get; set; }
}

internal static class ContactRateLimit
{
    /// <summary>
    /// Höchstens <see cref="ContactRateLimitOptions.MessagesPerHour"/> Nachrichten pro Stunde und Client. Nur POST zählt, die Seite selbst nicht.
    /// Der Client zählt nur im Arbeitsspeicher, IPv6 auf das /64-Netz gekürzt. Abgewiesen leitet auf das Formular mit `?kontakt=limit` um.
    /// </summary>
    public static IServiceCollection AddContactRateLimit(this IServiceCollection services, IConfiguration configuration) =>
        services.Configure<ContactRateLimitOptions>(configuration.GetSection(ContactRateLimitOptions.Section)).AddRateLimiter(options =>
        {
            options.AddPolicy(ContactForm.RateLimitPolicy, context => HttpMethods.IsPost(context.Request.Method)
                ? Limiter(context, context.RequestServices.GetRequiredService<IOptions<ContactRateLimitOptions>>().Value)
                : RateLimitPartition.GetNoLimiter("read"));
            options.OnRejected = (context, _) =>
            {
                var request = context.HttpContext.Request;
                context.HttpContext.Response.StatusCode = StatusCodes.Status303SeeOther;
                context.HttpContext.Response.Headers.Location = $"{request.PathBase}{request.Path}?kontakt=limit#kontakt";
                return ValueTask.CompletedTask;
            };
        });

    private static RateLimitPartition<string> Limiter(HttpContext context, ContactRateLimitOptions settings) =>
        RateLimitPartition.GetFixedWindowLimiter(ClientOf(context, settings.TrustCloudflareHeader), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = settings.MessagesPerHour,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
        });

    private static string ClientOf(HttpContext context, bool trustCloudflare)
    {
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
}
