using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Tauchplaetze.Web.CrossCutting.RateLimiting;

namespace Tauchplaetze.Web.Features.Accounts;

/// <summary>Einstellungen des Rate Limits für die Formulare zum Konto (`Accounts:RateLimit`, `infrastructure.md`).</summary>
internal class AccountFormsRateLimitOptions
{
    public const string Section = "Accounts:RateLimit";

    public int SignInPerHour { get; set; } = 20;

    public int FormsPerHour { get; set; } = 10;
}

/// <summary>
/// Begrenzt Anmeldung, Registrierung, «Passwort vergessen» und «Neues Passwort» pro Client und Stunde. Nur POST zählt.
/// Schützt vor Mail-Bombing, Passwort-Raten und dem Sperren fremder Konten (Lockout). Abgewiesen leitet auf dieselbe Seite mit `limit=1` um.
/// </summary>
internal static class AccountFormsRateLimit
{
    public const string Policy = "account-forms";

    public static void AddPolicy(RateLimiterOptions options) =>
        options.AddPolicy(Policy, context => HttpMethods.IsPost(context.Request.Method)
            ? Limiter(context, context.RequestServices.GetRequiredService<IOptions<AccountFormsRateLimitOptions>>().Value)
            : RateLimitPartition.GetNoLimiter("read"));

    public static bool Handles(HttpRequest request) =>
        request.Path.Value is { } path && path.Contains("/account/", StringComparison.Ordinal);

    public static void Reject(HttpContext context)
    {
        var request = context.Request;
        var query = request.QueryString.HasValue ? $"{request.QueryString.Value}&limit=1" : "?limit=1";
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = $"{request.PathBase}{request.Path}{query}";
    }

    private static RateLimitPartition<string> Limiter(HttpContext context, AccountFormsRateLimitOptions settings)
    {
        var signIn = context.Request.Path.Value?.EndsWith("/sign-in", StringComparison.Ordinal) == true;
        return RateLimitPartition.GetFixedWindowLimiter($"{(signIn ? "sign-in" : "forms")}:{RateLimits.ClientOf(context)}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = signIn ? settings.SignInPerHour : settings.FormsPerHour,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
        });
    }
}
