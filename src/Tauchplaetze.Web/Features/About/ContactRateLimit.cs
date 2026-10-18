using System.Threading.RateLimiting;

namespace Tauchplaetze.Web.Features.About;

internal static class ContactRateLimit
{
    private const int MessagesPerHour = 5;

    /// <summary>
    /// Höchstens 5 Nachrichten pro Stunde und IP-Adresse über das Kontaktformular. Nur POST zählt, die Seite selbst nicht.
    /// Die IP zählt nur im Arbeitsspeicher. Hinter Cloudflare gilt `CF-Connecting-IP`, sonst die Adresse der Verbindung.
    /// Abgewiesen leitet auf das Formular mit `?kontakt=limit` um.
    /// </summary>
    public static IServiceCollection AddContactRateLimit(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(ContactForm.RateLimitPolicy, context => HttpMethods.IsPost(context.Request.Method)
                ? RateLimitPartition.GetFixedWindowLimiter(ClientOf(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = MessagesPerHour,
                    Window = TimeSpan.FromHours(1),
                    QueueLimit = 0,
                })
                : RateLimitPartition.GetNoLimiter("read"));
            options.OnRejected = (context, _) =>
            {
                var request = context.HttpContext.Request;
                context.HttpContext.Response.StatusCode = StatusCodes.Status303SeeOther;
                context.HttpContext.Response.Headers.Location = $"{request.PathBase}{request.Path}?kontakt=limit#kontakt";
                return ValueTask.CompletedTask;
            };
        });

    private static string ClientOf(HttpContext context) =>
        context.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
