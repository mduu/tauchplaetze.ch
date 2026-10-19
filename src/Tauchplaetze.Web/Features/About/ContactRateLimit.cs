using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Tauchplaetze.Web.CrossCutting.RateLimiting;

namespace Tauchplaetze.Web.Features.About;

/// <summary>Einstellungen des Rate Limits für das Kontaktformular (`Contact`, `infrastructure.md`).</summary>
internal class ContactRateLimitOptions
{
    public const string Section = "Contact";

    public int MessagesPerHour { get; set; } = 5;

    /// <summary>
    /// Nur setzen, wenn die App ausschliesslich über Cloudflare erreichbar ist (Ingress auf die IP-Bereiche von Cloudflare beschränkt).
    /// Sonst kann jede Anfrage den Header fälschen. Gilt für alle Rate Limits.
    /// </summary>
    public bool TrustCloudflareHeader { get; set; }
}

internal static class ContactRateLimit
{
    /// <summary>
    /// Höchstens <see cref="ContactRateLimitOptions.MessagesPerHour"/> Nachrichten pro Stunde und Client. Nur POST zählt, die Seite selbst nicht.
    /// Der Client zählt nur im Arbeitsspeicher. Abgewiesen leitet auf das Formular mit `?kontakt=limit` um.
    /// </summary>
    public static void AddPolicy(RateLimiterOptions options) =>
        options.AddPolicy(ContactForm.RateLimitPolicy, context => HttpMethods.IsPost(context.Request.Method)
            ? Limiter(context, context.RequestServices.GetRequiredService<IOptions<ContactRateLimitOptions>>().Value)
            : RateLimitPartition.GetNoLimiter("read"));

    public static void Reject(HttpContext context)
    {
        var request = context.Request;
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = $"{request.PathBase}{request.Path}?kontakt=limit#kontakt";
    }

    private static RateLimitPartition<string> Limiter(HttpContext context, ContactRateLimitOptions settings) =>
        RateLimitPartition.GetFixedWindowLimiter(RateLimits.ClientOf(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = settings.MessagesPerHour,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
        });
}
