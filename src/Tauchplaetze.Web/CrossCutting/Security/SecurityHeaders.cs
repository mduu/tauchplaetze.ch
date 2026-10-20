namespace Tauchplaetze.Web.CrossCutting.Security;

/// <summary>
/// Security-Header für jede Antwort (Security-Review, 20.10.2026). Die CSP schützt nur vor Framing, Plugins und fremdem `base`.
/// Eine volle CSP braucht wegen der Inline-Skripte von Blazor und MudBlazor Nonces, später.
/// </summary>
internal static class SecurityHeaders
{
    public const string ContentSecurityPolicy = "frame-ancestors 'none'; object-src 'none'; base-uri 'self'";

    public const string PermissionsPolicy = "geolocation=(self), camera=(), microphone=(), payment=(), usb=()";

    /// <remarks>Antiforgery setzt sonst ein zweites `X-Frame-Options: SAMEORIGIN`. Blazor setzt keine eigene CSP (`ContentSecurityFrameAncestorsPolicy = null` in `Program.cs`).</remarks>
    public static IServiceCollection AddSecurityHeaders(this IServiceCollection services) =>
        services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(365))
            .AddAntiforgery(options => options.SuppressXFrameOptionsHeader = true);

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers.ContentSecurityPolicy = ContentSecurityPolicy;
            headers["Permissions-Policy"] = PermissionsPolicy;
            return next(context);
        });
}
